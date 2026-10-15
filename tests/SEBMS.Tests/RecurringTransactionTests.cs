using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SEBMS.Api.Controllers;
using SEBMS.Api.Services;
using SEBMS.Application.Recurring;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using SEBMS.Infrastructure.Persistence;
using static SEBMS.Tests.TestSupport;

namespace SEBMS.Tests;

public class RecurringTransactionTests
{
    private static DateOnly D(string s) => DateOnly.Parse(s);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private static RecurringTransaction Rule(string start, RecurrenceFrequency freq = RecurrenceFrequency.Monthly,
        string? end = null, bool active = true, string user = Alice, int category = Seed.Food, decimal amount = 100m) => new()
    {
        UserId = user, CategoryId = category, Type = category == Seed.Salary ? TransactionType.Income : TransactionType.Expense,
        Amount = amount, Frequency = freq, StartDate = D(start), EndDate = end is null ? null : D(end), IsActive = active
    };

    private static async Task<int> Run(AppDbContext db, string today, string? userId = null) =>
        await new RecurringTransactionProcessor(db).ProcessAsync(D(today), userId);

    // ---------- schedule maths ----------

    [Fact]
    public void Monthly_DoesNotDriftAfterAShortMonth()
    {
        var rule = Rule("2026-01-31");

        Assert.Equal(D("2026-01-31"), rule.Occurrence(0));
        Assert.Equal(D("2026-02-28"), rule.Occurrence(1));   // clamped
        Assert.Equal(D("2026-03-31"), rule.Occurrence(2));   // back on the 31st
    }

    [Fact]
    public void Weekly_AndYearly_StepCorrectly()
    {
        Assert.Equal(D("2026-01-22"), Rule("2026-01-01", RecurrenceFrequency.Weekly).Occurrence(3));
        Assert.Equal(D("2028-02-29"), Rule("2024-02-29", RecurrenceFrequency.Yearly).Occurrence(4));   // leap day
    }

    [Fact]
    public void IsFinished_OnlyOnceTheNextDateIsPastTheEnd()
    {
        var rule = Rule("2026-01-01", end: "2026-03-01");
        rule.GeneratedCount = 2;   // next = 1 Mar
        Assert.False(rule.IsFinished);
        rule.GeneratedCount = 3;   // next = 1 Apr
        Assert.True(rule.IsFinished);
    }

    // ---------- processor ----------

    [Fact]
    public async Task Process_CreatesEveryDueTransaction_AndCatchesUp()
    {
        var db = NewDb();
        db.RecurringTransactions.Add(Rule("2026-01-15", amount: 250m));
        await db.SaveChangesAsync();

        var created = await Run(db, "2026-04-20");

        Assert.Equal(4, created);   // 15 Jan, Feb, Mar, Apr
        var dates = await db.Transactions.OrderBy(t => t.TransactionDate).Select(t => t.TransactionDate).ToListAsync();
        Assert.Equal([D("2026-01-15"), D("2026-02-15"), D("2026-03-15"), D("2026-04-15")], dates);
        Assert.All(db.Transactions, t =>
        {
            Assert.Equal(250m, t.Amount);
            Assert.Equal(Alice, t.UserId);
            Assert.NotNull(t.RecurringTransactionId);
        });
        Assert.Equal(D("2026-05-15"), (await db.RecurringTransactions.SingleAsync()).NextDueDate);
    }

    [Fact]
    public async Task Process_IsIdempotent_AndDoesNotCreateFutureTransactions()
    {
        var db = NewDb();
        db.RecurringTransactions.Add(Rule("2026-05-01"));
        await db.SaveChangesAsync();

        Assert.Equal(0, await Run(db, "2026-04-30"));   // not due yet
        Assert.Equal(1, await Run(db, "2026-05-01"));   // due today
        Assert.Equal(0, await Run(db, "2026-05-01"));   // already done
        Assert.Equal(1, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Process_StopsAtTheEndDate_AndDeactivatesTheRule()
    {
        var db = NewDb();
        db.RecurringTransactions.Add(Rule("2026-01-01", end: "2026-03-10"));
        await db.SaveChangesAsync();

        var created = await Run(db, "2026-12-31");

        Assert.Equal(3, created);   // 1 Jan, 1 Feb, 1 Mar
        var rule = await db.RecurringTransactions.SingleAsync();
        Assert.False(rule.IsActive);
        Assert.True(rule.IsFinished);
    }

    [Fact]
    public async Task Process_SkipsPausedRules_AndCanBeLimitedToOneUser()
    {
        var db = NewDb();
        db.RecurringTransactions.AddRange(
            Rule("2026-01-01", active: false),
            Rule("2026-01-01", user: Alice),
            Rule("2026-01-01", user: Bob));
        await db.SaveChangesAsync();

        Assert.Equal(1, await Run(db, "2026-01-01", userId: Alice));
        Assert.Equal(1, await Run(db, "2026-01-01"));   // Bob's catches up; the paused rule never runs
        Assert.Equal(2, await db.Transactions.CountAsync());
        Assert.DoesNotContain(db.Transactions, t => t.UserId != Alice && t.UserId != Bob);
    }

    [Fact]
    public async Task Process_CapsAnAncientRule_AndContinuesOnTheNextRun()
    {
        var db = NewDb();
        db.RecurringTransactions.Add(Rule("2000-01-01", RecurrenceFrequency.Weekly));
        await db.SaveChangesAsync();

        var first = await Run(db, "2026-01-01");
        var second = await Run(db, "2026-01-01");

        Assert.Equal(RecurringTransactionProcessor.MaxPerRulePerRun, first);
        Assert.Equal(RecurringTransactionProcessor.MaxPerRulePerRun, second);
        Assert.Equal(first + second, await db.Transactions.Select(t => t.TransactionDate).Distinct().CountAsync());   // no duplicates
    }

    // ---------- dashboard notices ----------

    [Fact]
    public async Task Process_FlagsTransactionsForNotice_OnlyWhenAsked()
    {
        var db = NewDb();
        db.RecurringTransactions.Add(Rule("2026-01-15"));
        await db.SaveChangesAsync();
        await new RecurringTransactionProcessor(db).ProcessAsync(D("2026-01-20"));
        Assert.DoesNotContain(db.Transactions, t => t.NoticePending);   // the user's own saves never notify

        await new RecurringTransactionProcessor(db).ProcessAsync(D("2026-03-20"), notify: true);
        Assert.Equal(2, await db.Transactions.CountAsync(t => t.NoticePending));   // Feb + Mar only
    }

    [Fact]
    public async Task Notices_ListOnlyMyPendingOnes_AndDismissClearsThem()
    {
        var db = NewDb();
        db.RecurringTransactions.AddRange(Rule("2026-01-15"), Rule("2026-01-15", user: Bob));
        await db.SaveChangesAsync();
        await new RecurringTransactionProcessor(db).ProcessAsync(D("2026-03-20"), notify: true);

        var notices = (await Controller(db, Alice).Notices()).Value();
        Assert.Equal(3, notices.Count);
        Assert.Equal(3, notices.Items.Count);
        Assert.Equal(D("2026-03-15"), notices.Items[0].Date);   // newest first

        Assert.IsType<NoContentResult>(await Controller(db, Alice).DismissNotices());

        Assert.Equal(0, (await Controller(db, Alice).Notices()).Value().Count);
        Assert.Equal(3, (await Controller(db, Bob).Notices()).Value().Count);   // Bob's are untouched
    }

    // ---------- API controller ----------

    private static RecurringController Controller(AppDbContext db, string user) =>
        new RecurringController(db, new RecurringTransactionProcessor(db)).As(user);

    private static RecurringRequest Request(DateOnly start, DateOnly? end = null, int category = Seed.Food,
        TransactionType type = TransactionType.Expense, RecurrenceFrequency freq = RecurrenceFrequency.Monthly, bool active = true) =>
        new(category, type, 500m, freq, start, end, "Gym", active);

    [Fact]
    public async Task Create_StartingToday_RecordsTheFirstTransactionImmediately()
    {
        var db = NewDb();

        var created = await Controller(db, Alice).Create(Request(Today));

        var response = Assert.IsType<RecurringResponse>(((CreatedAtActionResult)created.Result!).Value);
        Assert.Equal(1, response.GeneratedCount);
        Assert.Equal(Today.AddMonths(1), response.NextDueDate);
        var tx = await db.Transactions.SingleAsync();
        Assert.Equal(Today, tx.TransactionDate);
        Assert.Equal(response.Id, tx.RecurringTransactionId);
    }

    [Fact]
    public async Task Create_StartingInTheFuture_RecordsNothingYet()
    {
        var db = NewDb();

        var response = (await Controller(db, Alice).Create(Request(Today.AddDays(10)))).Value();

        Assert.Equal(0, response.GeneratedCount);
        Assert.Equal(Today.AddDays(10), response.NextDueDate);
        Assert.Empty(db.Transactions);
    }

    [Fact]
    public async Task Create_RejectsBadInput()
    {
        var db = NewDb();
        var c = Controller(db, Alice);

        Assert.IsType<BadRequestObjectResult>((await c.Create(Request(Today, end: Today.AddDays(-1)))).Result);                 // end < start
        Assert.IsType<BadRequestObjectResult>((await c.Create(Request(Today, category: Seed.Salary))).Result);                 // type mismatch
        Assert.IsType<BadRequestObjectResult>((await c.Create(Request(Today, category: 9999))).Result);                         // unknown category
        Assert.Empty(db.RecurringTransactions);
    }

    [Fact]
    public async Task Rules_ArePrivateToTheirOwner()
    {
        var db = NewDb();
        var id = (await Controller(db, Alice).Create(Request(Today.AddDays(5)))).Value().Id;

        Assert.Empty((await Controller(db, Bob).GetAll()).Value());
        Assert.IsType<NotFoundResult>((await Controller(db, Bob).Get(id)).Result);
        Assert.IsType<NotFoundResult>((await Controller(db, Bob).Update(id, Request(Today))).Result);
        Assert.IsType<NotFoundResult>(await Controller(db, Bob).Delete(id));
        Assert.Single((await Controller(db, Alice).GetAll()).Value());
    }

    [Fact]
    public async Task Update_ChangingTheAmount_KeepsTheScheduleAndEarlierTransactions()
    {
        var db = NewDb();
        var c = Controller(db, Alice);
        var id = (await c.Create(Request(Today))).Result is CreatedAtActionResult { Value: RecurringResponse r } ? r.Id : 0;

        var updated = (await c.Update(id, Request(Today) with { Amount = 750m })).Value();

        Assert.Equal(750m, updated.Amount);
        Assert.Equal(1, updated.GeneratedCount);           // not regenerated
        Assert.Equal(1, await db.Transactions.CountAsync());
        Assert.Equal(500m, (await db.Transactions.SingleAsync()).Amount);   // the one already created is unchanged
    }

    [Fact]
    public async Task Update_ChangingTheStartDate_RestartsFromTodayWithoutBackfilling()
    {
        var db = NewDb();
        var c = Controller(db, Alice);
        var id = (await c.Create(Request(Today.AddMonths(-3).AddDays(1)))).Result is CreatedAtActionResult { Value: RecurringResponse r } ? r.Id : 0;
        var before = await db.Transactions.CountAsync();   // caught up on creation
        Assert.True(before >= 3);

        await c.Update(id, Request(Today.AddMonths(-6)));   // earlier start date: past dates must not be re-created

        Assert.Equal(before, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Pausing_StopsGeneration_AndResumingContinues()
    {
        var db = NewDb();
        var c = Controller(db, Alice);
        var id = (await c.Create(Request(Today.AddDays(-1), freq: RecurrenceFrequency.Weekly, active: false))).Value().Id;
        Assert.Empty(db.Transactions);

        await c.Update(id, Request(Today.AddDays(-1), freq: RecurrenceFrequency.Weekly, active: true));

        Assert.Equal(1, await db.Transactions.CountAsync());   // yesterday's occurrence is caught up on resume
    }

    [Fact]
    public async Task Delete_RemovesTheRule_ButNotItsTransactions()
    {
        var db = NewDb();
        var c = Controller(db, Alice);
        var id = (await c.Create(Request(Today))).Result is CreatedAtActionResult { Value: RecurringResponse r } ? r.Id : 0;
        _ = await db.Transactions.ToListAsync();   // tracked, so the link is cleared like the database's ON DELETE SET NULL

        Assert.IsType<NoContentResult>(await c.Delete(id));

        Assert.Empty(db.RecurringTransactions);
        Assert.Equal(1, await db.Transactions.CountAsync());
        Assert.Null((await db.Transactions.SingleAsync()).RecurringTransactionId);
    }

    [Fact]
    public async Task Run_GeneratesWhatIsDueForTheCaller()
    {
        var db = NewDb();
        db.RecurringTransactions.Add(Rule(Today.AddDays(-2).ToString("yyyy-MM-dd"), RecurrenceFrequency.Weekly));
        await db.SaveChangesAsync();

        var result = (await Controller(db, Alice).Run()).Value();

        Assert.Equal(1, result.Created);
    }
}

using Microsoft.AspNetCore.Mvc;
using SEBMS.Api.Controllers;
using SEBMS.Application.Transactions;
using SEBMS.Domain.Enums;
using static SEBMS.Tests.TestSupport;

namespace SEBMS.Tests;

public class TransactionsControllerTests
{
    private static TransactionRequest Expense(decimal amount = 100, string date = "2026-10-05", int category = Seed.Food, string? note = null, string currency = "INR") =>
        new(category, TransactionType.Expense, amount, DateOnly.Parse(date), note, currency);

    private static int IdOf(ActionResult<TransactionResponse> created) =>
        ((TransactionResponse)((CreatedAtActionResult)created.Result!).Value!).Id;

    [Fact]
    public async Task Create_ReturnsCreated_WithCategoryName()
    {
        var c = new TransactionsController(NewDb()).As(Alice);

        var result = await c.Create(Expense(250.5m, note: "Lunch"));

        var body = (TransactionResponse)((CreatedAtActionResult)result.Result!).Value!;
        Assert.Equal(250.5m, body.Amount);
        Assert.Equal("Food and Dining", body.CategoryName);
        Assert.Equal("Lunch", body.Note);
    }

    [Fact]
    public async Task Create_UppercasesCurrencyCode()
    {
        var c = new TransactionsController(NewDb()).As(Alice);

        var body = (TransactionResponse)((CreatedAtActionResult)(await c.Create(Expense(currency: "inr"))).Result!).Value!;

        Assert.Equal("INR", body.CurrencyCode);
    }

    [Fact]
    public async Task Create_TypeDoesNotMatchCategory_ReturnsBadRequest()
    {
        var c = new TransactionsController(NewDb()).As(Alice);

        // Food is an Expense category, but the request says Income.
        var result = await c.Create(new TransactionRequest(Seed.Food, TransactionType.Income, 10, new DateOnly(2026, 10, 5), null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_UnknownCategory_ReturnsBadRequest()
    {
        var c = new TransactionsController(NewDb()).As(Alice);

        Assert.IsType<BadRequestObjectResult>((await c.Create(Expense(category: 9999))).Result);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnTransactions_NewestFirst()
    {
        var db = NewDb();
        var alice = new TransactionsController(db).As(Alice);
        await alice.Create(Expense(date: "2026-10-01"));
        await alice.Create(Expense(date: "2026-10-09"));
        await new TransactionsController(db).As(Bob).Create(Expense(date: "2026-10-05"));

        var list = (await alice.GetAll(null, null, null, null)).Value().ToList();

        Assert.Equal(2, list.Count);
        Assert.Equal(new DateOnly(2026, 10, 9), list[0].TransactionDate);
    }

    [Fact]
    public async Task GetAll_AppliesFilters()
    {
        var c = new TransactionsController(NewDb()).As(Alice);
        await c.Create(Expense(10, "2026-09-15"));
        await c.Create(Expense(20, "2026-10-05", Seed.Transport));
        await c.Create(Expense(30, "2026-10-20"));
        await c.Create(new TransactionRequest(Seed.Salary, TransactionType.Income, 99, new DateOnly(2026, 10, 1), null));

        var inOctober = (await c.GetAll(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, null)).Value();
        var transport = (await c.GetAll(null, null, Seed.Transport, null)).Value();
        var income = (await c.GetAll(null, null, null, TransactionType.Income)).Value();
        var boundary = (await c.GetAll(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5), null, null)).Value();

        Assert.Equal(3, inOctober.Count());
        Assert.Equal(20, Assert.Single(transport).Amount);
        Assert.Equal(99, Assert.Single(income).Amount);
        Assert.Single(boundary);   // from/to are inclusive
    }

    [Fact]
    public async Task Get_OtherUsersTransaction_ReturnsNotFound()
    {
        var db = NewDb();
        var id = IdOf(await new TransactionsController(db).As(Alice).Create(Expense()));

        var result = await new TransactionsController(db).As(Bob).Get(id);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var c = new TransactionsController(NewDb()).As(Alice);
        var id = IdOf(await c.Create(Expense(100)));

        var updated = (await c.Update(id, Expense(175, "2026-10-07", Seed.Transport, "Cab"))).Value();

        Assert.Equal(175, updated.Amount);
        Assert.Equal(Seed.Transport, updated.CategoryId);
        Assert.Equal("Cab", updated.Note);
    }

    [Fact]
    public async Task Update_OtherUsersTransaction_ReturnsNotFound()
    {
        var db = NewDb();
        var id = IdOf(await new TransactionsController(db).As(Alice).Create(Expense()));

        var result = await new TransactionsController(db).As(Bob).Update(id, Expense(1));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_IsSoftDelete_AndHidesTransaction()
    {
        var db = NewDb();
        var c = new TransactionsController(db).As(Alice);
        var id = IdOf(await c.Create(Expense()));

        Assert.IsType<NoContentResult>(await c.Delete(id));

        Assert.Empty((await c.GetAll(null, null, null, null)).Value());
        Assert.IsType<NotFoundResult>((await c.Get(id)).Result);
        Assert.True(db.Transactions.Single().IsDeleted);   // row is kept
        Assert.IsType<NotFoundResult>(await c.Delete(id)); // already deleted
    }

    [Fact]
    public async Task Delete_OtherUsersTransaction_ReturnsNotFound()
    {
        var db = NewDb();
        var id = IdOf(await new TransactionsController(db).As(Alice).Create(Expense()));

        Assert.IsType<NotFoundResult>(await new TransactionsController(db).As(Bob).Delete(id));
        Assert.False(db.Transactions.Single().IsDeleted);
    }
}

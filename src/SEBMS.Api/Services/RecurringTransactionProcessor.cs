using Microsoft.EntityFrameworkCore;
using SEBMS.Domain.Entities;
using SEBMS.Infrastructure.Persistence;

namespace SEBMS.Api.Services;

/// <summary>Turns recurring rules that have fallen due into real transactions, catching up on any missed periods.</summary>
public class RecurringTransactionProcessor(AppDbContext db)
{
    /// <summary>Upper bound per rule per run, so a rule with an ancient start date cannot flood the table in one go.</summary>
    public const int MaxPerRulePerRun = 400;

    /// <returns>How many transactions were created.</returns>
    public async Task<int> ProcessAsync(DateOnly today, string? userId = null, CancellationToken ct = default)
    {
        var rules = await db.RecurringTransactions
            .Where(r => r.IsActive && (userId == null || r.UserId == userId))
            .ToListAsync(ct);

        var created = 0;
        foreach (var rule in rules)
        {
            var added = new List<Transaction>();
            while (added.Count < MaxPerRulePerRun && !rule.IsFinished && rule.NextDueDate <= today)
            {
                added.Add(new Transaction
                {
                    UserId = rule.UserId,
                    CategoryId = rule.CategoryId,
                    Type = rule.Type,
                    Amount = rule.Amount,
                    CurrencyCode = rule.CurrencyCode,
                    Note = rule.Note,
                    TransactionDate = rule.NextDueDate,
                    RecurringTransactionId = rule.Id
                });
                rule.GeneratedCount++;
            }
            if (rule.IsFinished) rule.IsActive = false;
            if (added.Count == 0 && rule.IsActive) continue;

            db.Transactions.AddRange(added);
            rule.UpdatedAt = DateTime.UtcNow;
            try
            {
                await db.SaveChangesAsync(ct);
                created += added.Count;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another run (or an edit) changed this rule first; it has produced these transactions already.
                foreach (var entry in db.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
            }
        }
        return created;
    }
}

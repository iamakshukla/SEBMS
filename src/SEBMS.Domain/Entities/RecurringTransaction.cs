using SEBMS.Domain.Enums;

namespace SEBMS.Domain.Entities;

/// <summary>
/// A schedule that creates a real <see cref="Transaction"/> each time it falls due. Occurrence n is computed from
/// <see cref="StartDate"/> (not from the previous one), so a monthly rule starting on the 31st does not drift to the 28th.
/// </summary>
public class RecurringTransaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "INR";
    public string? Note { get; set; }
    public RecurrenceFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Occurrences already turned into transactions. Doubles as a concurrency token so two runs cannot both generate the same one.</summary>
    public int GeneratedCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Category? Category { get; set; }

    public DateOnly Occurrence(int index) => Frequency switch
    {
        RecurrenceFrequency.Weekly => StartDate.AddDays(7 * index),
        RecurrenceFrequency.Monthly => StartDate.AddMonths(index),
        RecurrenceFrequency.Yearly => StartDate.AddYears(index),
        _ => throw new InvalidOperationException($"Unknown frequency {Frequency}.")
    };

    public DateOnly NextDueDate => Occurrence(GeneratedCount);

    /// <summary>True once the next occurrence would fall after the end date.</summary>
    public bool IsFinished => EndDate is { } end && NextDueDate > end;

    /// <summary>Index of the first occurrence on or after <paramref name="date"/> (used when the schedule is changed).</summary>
    public int FirstOccurrenceOnOrAfter(DateOnly date)
    {
        var n = 0;
        while (n < 100_000 && Occurrence(n) < date) n++;
        return n;
    }
}

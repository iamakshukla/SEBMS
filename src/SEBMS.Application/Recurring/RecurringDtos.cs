using System.ComponentModel.DataAnnotations;
using SEBMS.Domain.Enums;

namespace SEBMS.Application.Recurring;

public record RecurringRequest(
    [Required] int CategoryId,
    [Required] TransactionType Type,
    [Range(0.01, 9999999999.99)] decimal Amount,
    [Required] RecurrenceFrequency Frequency,
    [Required] DateOnly StartDate,
    DateOnly? EndDate,
    [StringLength(500)] string? Note,
    bool IsActive = true,
    [StringLength(3, MinimumLength = 3)] string CurrencyCode = "INR");

/// <summary>NextDueDate is null once the rule is paused or finished (its next date would be after EndDate).</summary>
public record RecurringResponse(
    int Id, int CategoryId, string? CategoryName, TransactionType Type, decimal Amount, string CurrencyCode,
    string? Note, RecurrenceFrequency Frequency, DateOnly StartDate, DateOnly? EndDate, bool IsActive,
    DateOnly? NextDueDate, int GeneratedCount, bool IsFinished);

public record RunRecurringResponse(int Created);

/// <summary>A transaction the background worker created that the user has not yet acknowledged.</summary>
public record RecurringNoticeItem(
    int TransactionId, DateOnly Date, TransactionType Type, decimal Amount, string CurrencyCode,
    string? CategoryName, string? Note);

/// <summary>Count is the full number pending; Items holds the most recent few.</summary>
public record RecurringNoticeResponse(int Count, IReadOnlyList<RecurringNoticeItem> Items);

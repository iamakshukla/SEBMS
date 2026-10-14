using SEBMS.Domain.Enums;

namespace SEBMS.Domain.Entities;

public class Transaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? Note { get; set; }
    public string CurrencyCode { get; set; } = "INR";
    public bool IsDeleted { get; set; }

    /// <summary>The recurring rule that generated this transaction, if any (cleared if the rule is deleted).</summary>
    public int? RecurringTransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Category? Category { get; set; }
}
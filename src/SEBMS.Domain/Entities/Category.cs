using SEBMS.Domain.Enums;

namespace SEBMS.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string? UserId { get; set; }          // null for shared default categories
    public string Name { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public bool IsDefault { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
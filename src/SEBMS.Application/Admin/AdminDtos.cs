using SEBMS.Domain.Enums;

namespace SEBMS.Application.Admin;

public record AdminUserResponse(
    string Id, string Email, string? DisplayName, bool IsAdmin, DateTime CreatedAt,
    int TransactionCount, int BudgetCount);

public record AdminTransactionResponse(
    int Id, string UserEmail, string? CategoryName, TransactionType Type, decimal Amount,
    DateOnly TransactionDate, string? Note, string CurrencyCode);

public record AdminBudgetResponse(
    int Id, string UserEmail, string? CategoryName, short BudgetYear, byte BudgetMonth, decimal Amount);

/// <summary>OwnerEmail is null for the shared default categories.</summary>
public record AdminCategoryResponse(int Id, string Name, TransactionType Type, bool IsDefault, string? OwnerEmail);

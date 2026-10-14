using SEBMS.Domain.Enums;

namespace SEBMS.Application.Admin;

public record AdminUserResponse(
    string Id, string Email, string? DisplayName, bool IsAdmin, DateTime CreatedAt,
    int TransactionCount, int BudgetCount);

/// <summary>CategoryId and UserId (the owner) are what the admin edit forms need; the rest is for display.</summary>
public record AdminTransactionResponse(
    int Id, string UserEmail, string? CategoryName, TransactionType Type, decimal Amount,
    DateOnly TransactionDate, string? Note, string CurrencyCode,
    int CategoryId = 0, string UserId = "");

public record AdminBudgetResponse(
    int Id, string UserEmail, string? CategoryName, short BudgetYear, byte BudgetMonth, decimal Amount,
    int CategoryId = 0, string UserId = "");

public record AuditEntryResponse(
    long Id, DateTime OccurredAtUtc, string AdminEmail, string Action, string EntityType, string EntityId,
    string TargetUserId, string TargetEmail, string Summary, string? Details);

public record AuditPageResponse(IReadOnlyList<AuditEntryResponse> Items, int Total, int Page, int PageSize);

/// <summary>OwnerEmail is null for the shared default categories.</summary>
public record AdminCategoryResponse(int Id, string Name, TransactionType Type, bool IsDefault, string? OwnerEmail);

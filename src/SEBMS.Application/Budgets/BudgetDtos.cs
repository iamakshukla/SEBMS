using System.ComponentModel.DataAnnotations;

namespace SEBMS.Application.Budgets;

public record BudgetRequest(
    [Required] int CategoryId,
    [Range(2000, 2100)] short Year,
    [Range(1, 12)] byte Month,
    [Range(typeof(decimal), "0.01", "9999999999.99")] decimal Amount);

public record BudgetResponse(
    int Id, int CategoryId, string? CategoryName, short Year, byte Month,
    decimal Amount, decimal Spent, decimal Remaining);

using System.ComponentModel.DataAnnotations;
using SEBMS.Domain.Enums;

namespace SEBMS.Application.Transactions;

public record TransactionRequest(
    [Required] int CategoryId,
    [Required] TransactionType Type,
    [Range(0.01, 9999999999.99)] decimal Amount,
    [Required] DateOnly TransactionDate,
    [StringLength(500)] string? Note,
    [StringLength(3, MinimumLength = 3)] string CurrencyCode = "INR");

public record TransactionResponse(
    int Id,
    int CategoryId,
    string? CategoryName,
    TransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Note,
    string CurrencyCode);

using System.ComponentModel.DataAnnotations;
using SEBMS.Domain.Enums;

namespace SEBMS.Application.Categories;

public record CategoryRequest(
    [Required, StringLength(50, MinimumLength = 1)] string Name,
    [Required] TransactionType Type);

public record CategoryResponse(int Id, string Name, TransactionType Type, bool IsDefault);

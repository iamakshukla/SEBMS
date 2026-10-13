using System.ComponentModel.DataAnnotations;
using SEBMS.Application.Categories;
using SEBMS.Application.Transactions;
using SEBMS.Domain.Enums;

namespace SEBMS.Web.Models;

public class TransactionListViewModel
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? CategoryId { get; set; }
    public TransactionType? Type { get; set; }
    public IReadOnlyList<CategoryResponse> Categories { get; set; } = [];
    public IReadOnlyList<TransactionResponse> Items { get; set; } = [];

    public decimal Income => Items.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
    public decimal Expense => Items.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
}

public class TransactionFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required, Range(0.01, 9999999999.99, ErrorMessage = "Enter an amount greater than 0.")]
    public decimal? Amount { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateOnly? TransactionDate { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public IReadOnlyList<CategoryResponse> Categories { get; set; } = [];
}

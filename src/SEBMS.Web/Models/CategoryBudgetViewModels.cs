using System.ComponentModel.DataAnnotations;
using SEBMS.Application.Budgets;
using SEBMS.Application.Categories;
using SEBMS.Domain.Enums;

namespace SEBMS.Web.Models;

public class CategoryFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = "";

    [Required]
    public TransactionType? Type { get; set; }
}

public class BudgetListViewModel
{
    public short Year { get; set; }
    public byte Month { get; set; }
    public IReadOnlyList<BudgetResponse> Items { get; set; } = [];

    public decimal TotalBudget => Items.Sum(b => b.Amount);
    public decimal TotalSpent => Items.Sum(b => b.Spent);
}

public class BudgetFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required, Range(2000, 2100)]
    public short? Year { get; set; }

    [Required, Range(1, 12)]
    public byte? Month { get; set; }

    [Required, Range(0.01, 9999999999.99, ErrorMessage = "Enter an amount greater than 0.")]
    [Display(Name = "Budget amount")]
    public decimal? Amount { get; set; }

    public IReadOnlyList<CategoryResponse> Categories { get; set; } = [];
}

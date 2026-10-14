using System.ComponentModel.DataAnnotations;
using SEBMS.Application.Categories;
using SEBMS.Domain.Enums;

namespace SEBMS.Web.Models;

public class RecurringFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required, Range(0.01, 9999999999.99, ErrorMessage = "Enter an amount greater than 0.")]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Choose how often it repeats.")]
    [Display(Name = "Repeats")]
    public RecurrenceFrequency? Frequency { get; set; } = RecurrenceFrequency.Monthly;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Starts on")]
    public DateOnly? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Ends on (optional)")]
    public DateOnly? EndDate { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;

    public IReadOnlyList<CategoryResponse> Categories { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EndDate is { } end && StartDate is { } start && end < start)
            yield return new ValidationResult("The end date cannot be before the start date.", [nameof(EndDate)]);
    }
}

using System.ComponentModel.DataAnnotations;
using SEBMS.Application.Auth;
using SEBMS.Application.Budgets;
using SEBMS.Application.Categories;
using SEBMS.Application.Transactions;
using SEBMS.Domain.Enums;

namespace SEBMS.Tests;

/// <summary>The API relies on these attributes ([ApiController] returns 400 when they fail).</summary>
public class DtoValidationTests
{
    private static List<ValidationResult> Validate(object dto)
    {
        // Records put attributes on constructor parameters, so validate each property by hand.
        var results = new List<ValidationResult>();
        var ctor = dto.GetType().GetConstructors().Single();
        foreach (var p in ctor.GetParameters())
        {
            var prop = dto.GetType().GetProperty(p.Name!)!;
            var ctx = new ValidationContext(dto) { MemberName = p.Name };
            var attrs = p.GetCustomAttributes(typeof(ValidationAttribute), true).Cast<ValidationAttribute>();
            Validator.TryValidateValue(prop.GetValue(dto), ctx, results, attrs);
        }
        return results;
    }

    [Theory]
    [InlineData("a@b.com", "12345678", true)]
    [InlineData("a@b.com", "1234567", false)]     // too short
    [InlineData("not-an-email", "12345678", false)]
    [InlineData("", "12345678", false)]
    public void RegisterRequest_Validates(string email, string password, bool valid) =>
        Assert.Equal(valid, Validate(new RegisterRequest(email, password, null)).Count == 0);

    [Theory]
    [InlineData(0.01, true)]
    [InlineData(1500.5, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    public void TransactionRequest_AmountMustBePositive(double amount, bool valid) =>
        Assert.Equal(valid, Validate(new TransactionRequest(1, TransactionType.Expense, (decimal)amount, new DateOnly(2026, 10, 5), null)).Count == 0);

    [Theory]
    [InlineData("INR", true)]
    [InlineData("IN", false)]
    [InlineData("INRR", false)]
    public void TransactionRequest_CurrencyMustBeThreeLetters(string currency, bool valid) =>
        Assert.Equal(valid, Validate(new TransactionRequest(1, TransactionType.Expense, 10, new DateOnly(2026, 10, 5), null, currency)).Count == 0);

    [Fact]
    public void TransactionRequest_NoteIsLimitedTo500Characters()
    {
        Assert.Empty(Validate(new TransactionRequest(1, TransactionType.Expense, 10, new DateOnly(2026, 10, 5), new string('x', 500))));
        Assert.NotEmpty(Validate(new TransactionRequest(1, TransactionType.Expense, 10, new DateOnly(2026, 10, 5), new string('x', 501))));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(12, true)]
    [InlineData(0, false)]
    [InlineData(13, false)]
    public void BudgetRequest_MonthMustBe1To12(int month, bool valid) =>
        Assert.Equal(valid, Validate(new BudgetRequest(1, 2026, (byte)month, 100)).Count == 0);

    [Theory]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void BudgetRequest_AmountMustBePositive(double amount, bool valid) =>
        Assert.Equal(valid, Validate(new BudgetRequest(1, 2026, 10, (decimal)amount)).Count == 0);

    [Theory]
    [InlineData("Pets", true)]
    [InlineData("", false)]
    public void CategoryRequest_NameIsRequired(string name, bool valid) =>
        Assert.Equal(valid, Validate(new CategoryRequest(name, TransactionType.Expense)).Count == 0);

    [Fact]
    public void CategoryRequest_NameIsLimitedTo50Characters()
    {
        Assert.Empty(Validate(new CategoryRequest(new string('x', 50), TransactionType.Expense)));
        Assert.NotEmpty(Validate(new CategoryRequest(new string('x', 51), TransactionType.Expense)));
    }
}

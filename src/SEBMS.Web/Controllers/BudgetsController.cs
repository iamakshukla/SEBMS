using Microsoft.AspNetCore.Mvc;
using SEBMS.Application.Budgets;
using SEBMS.Application.Categories;
using SEBMS.Domain.Enums;
using SEBMS.Web.Models;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

public class BudgetsController(ApiClient api) : AppController(api)
{
    public async Task<IActionResult> Index(short? year, byte? month)
    {
        var (y, m) = Normalize(year, month);
        var result = await Api.GetAsync<List<BudgetResponse>>($"api/budgets?year={y}&month={m}");
        if (await FailAsync(result) is { } failure) return failure;
        return View(new BudgetListViewModel { Year = y, Month = m, Items = result.Data! });
    }

    [HttpGet]
    public async Task<IActionResult> Create(short? year, byte? month)
    {
        var (y, m) = Normalize(year, month);
        var categories = await ExpenseCategoriesAsync();
        if (await FailAsync(categories) is { } failure) return failure;
        return View("Form", new BudgetFormViewModel { Year = y, Month = m, Categories = categories.Data!.Where(c => c.Type == TransactionType.Expense).ToList() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(BudgetFormViewModel model) => SaveAsync(model);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var budget = await Api.GetAsync<BudgetResponse>($"api/budgets/{id}");
        if (await FailAsync(budget) is { } f1) return f1;
        var categories = await ExpenseCategoriesAsync();
        if (await FailAsync(categories) is { } f2) return f2;

        var b = budget.Data!;
        return View("Form", new BudgetFormViewModel
        {
            Id = id, CategoryId = b.CategoryId, Year = b.Year, Month = b.Month, Amount = b.Amount,
            Categories = categories.Data!.Where(c => c.Type == TransactionType.Expense).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, BudgetFormViewModel model)
    {
        model.Id = id;
        return SaveAsync(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, short year, byte month)
    {
        var result = await Api.DeleteAsync($"api/budgets/{id}");
        if (result.Unauthorized) return await SignOutAsync();
        TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "Budget deleted." : result.Errors.FirstOrDefault();
        return RedirectToAction(nameof(Index), new { year, month });
    }

    private async Task<IActionResult> SaveAsync(BudgetFormViewModel model)
    {
        var categories = await ExpenseCategoriesAsync();
        if (await FailAsync(categories) is { } failure) return failure;
        model.Categories = categories.Data!.Where(c => c.Type == TransactionType.Expense).ToList();

        if (!ModelState.IsValid) return View("Form", model);

        var body = new BudgetRequest(model.CategoryId!.Value, model.Year!.Value, model.Month!.Value, model.Amount!.Value);
        var result = model.Id is null
            ? await Api.PostAsync<BudgetResponse>("api/budgets", body)
            : await Api.PutAsync<BudgetResponse>($"api/budgets/{model.Id}", body);

        if (result.Unauthorized) return await SignOutAsync();
        if (!result.Ok)
        {
            AddErrors(result.Errors);
            return View("Form", model);
        }

        TempData["Success"] = model.Id is null ? "Budget added." : "Budget updated.";
        return RedirectToAction(nameof(Index), new { year = model.Year, month = model.Month });
    }

    private Task<ApiResult<List<CategoryResponse>>> ExpenseCategoriesAsync() =>
        Api.GetAsync<List<CategoryResponse>>("api/categories");

    private static (short Year, byte Month) Normalize(short? year, byte? month)
    {
        var today = DateTime.Today;
        return (year is >= 2000 and <= 2100 ? year.Value : (short)today.Year,
                month is >= 1 and <= 12 ? month.Value : (byte)today.Month);
    }
}

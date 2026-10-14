using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBMS.Application.Admin;
using SEBMS.Application.Budgets;
using SEBMS.Application.Categories;
using SEBMS.Application.Transactions;
using SEBMS.Domain.Enums;
using SEBMS.Web.Models;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

/// <summary>Admin-only pages. The role check here hides the UI; the API enforces it on every call.</summary>
[Authorize(Roles = "Admin")]
public class AdminController(ApiClient api) : AppController(api)
{
    // ---------- users ----------

    public async Task<IActionResult> Index()
    {
        var result = await Api.GetAsync<List<AdminUserResponse>>("api/admin/users");
        if (await FailAsync(result) is { } failure) return failure;
        return View(result.Data!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var result = await Api.DeleteAsync($"api/admin/users/{Uri.EscapeDataString(id)}");
        if (result.Unauthorized) return await SignOutAsync();
        TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "User and all their data deleted." : result.Errors.FirstOrDefault();
        return RedirectToAction(nameof(Index));
    }

    // ---------- audit log ----------

    public async Task<IActionResult> Audit(string? userId, int page = 1)
    {
        var url = $"api/admin/audit?page={Math.Max(1, page)}" + (userId is null ? "" : $"&userId={Uri.EscapeDataString(userId)}");
        var result = await Api.GetAsync<AuditPageResponse>(url);
        if (await FailAsync(result) is { } failure) return failure;
        ViewBag.UserId = userId;
        return View(result.Data!);
    }

    // ---------- lists ----------

    public async Task<IActionResult> Transactions(string? userId)
    {
        var result = await Api.GetAsync<List<AdminTransactionResponse>>(
            "api/admin/transactions" + (userId is null ? "" : $"?userId={Uri.EscapeDataString(userId)}"));
        if (await FailAsync(result) is { } failure) return failure;
        ViewBag.UserId = userId;
        return View(result.Data!);
    }

    public async Task<IActionResult> Budgets(string? userId)
    {
        var result = await Api.GetAsync<List<AdminBudgetResponse>>(
            "api/admin/budgets" + (userId is null ? "" : $"?userId={Uri.EscapeDataString(userId)}"));
        if (await FailAsync(result) is { } failure) return failure;
        ViewBag.UserId = userId;
        return View(result.Data!);
    }

    public async Task<IActionResult> Categories()
    {
        var result = await Api.GetAsync<List<AdminCategoryResponse>>("api/admin/categories");
        if (await FailAsync(result) is { } failure) return failure;
        return View(result.Data!);
    }

    // ---------- edit / delete a user's transaction ----------

    [HttpGet]
    public async Task<IActionResult> EditTransaction(int id, string? userId)
    {
        var tx = await Api.GetAsync<AdminTransactionResponse>($"api/admin/transactions/{id}");
        if (await FailAsync(tx) is { } f1) return f1;
        var categories = await OwnerCategoriesAsync(tx.Data!.UserId);
        if (await FailAsync(categories) is { } f2) return f2;

        ViewBag.OwnerEmail = tx.Data.UserEmail;
        ViewBag.ReturnUserId = userId;
        var t = tx.Data;
        return View(new TransactionFormViewModel
        {
            Id = t.Id, CategoryId = t.CategoryId, Amount = t.Amount,
            TransactionDate = t.TransactionDate, Note = t.Note, Categories = categories.Data!
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTransaction(int id, string? userId, TransactionFormViewModel model)
    {
        model.Id = id;
        var tx = await Api.GetAsync<AdminTransactionResponse>($"api/admin/transactions/{id}");
        if (await FailAsync(tx) is { } f1) return f1;
        var categories = await OwnerCategoriesAsync(tx.Data!.UserId);
        if (await FailAsync(categories) is { } f2) return f2;
        model.Categories = categories.Data!;
        ViewBag.OwnerEmail = tx.Data.UserEmail;
        ViewBag.ReturnUserId = userId;

        // The category decides the type, so the two can never disagree.
        var category = model.Categories.FirstOrDefault(c => c.Id == model.CategoryId);
        if (model.CategoryId is not null && category is null)
            ModelState.AddModelError(nameof(model.CategoryId), "Choose a valid category.");
        if (!ModelState.IsValid) return View(model);

        var body = new TransactionRequest(category!.Id, category.Type, model.Amount!.Value, model.TransactionDate!.Value,
            string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(), tx.Data.CurrencyCode);
        var result = await Api.PutAsync<AdminTransactionResponse>($"api/admin/transactions/{id}", body);
        if (result.Unauthorized) return await SignOutAsync();
        if (!result.Ok)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        TempData["Success"] = $"Transaction of {tx.Data.UserEmail} updated.";
        return RedirectToAction(nameof(Transactions), new { userId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransaction(int id, string? userId)
    {
        var result = await Api.DeleteAsync($"api/admin/transactions/{id}");
        if (result.Unauthorized) return await SignOutAsync();
        TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "Transaction deleted." : result.Errors.FirstOrDefault();
        return RedirectToAction(nameof(Transactions), new { userId });
    }

    // ---------- edit / delete a user's budget ----------

    [HttpGet]
    public async Task<IActionResult> EditBudget(int id, string? userId)
    {
        var budget = await Api.GetAsync<AdminBudgetResponse>($"api/admin/budgets/{id}");
        if (await FailAsync(budget) is { } f1) return f1;
        var categories = await OwnerCategoriesAsync(budget.Data!.UserId);
        if (await FailAsync(categories) is { } f2) return f2;

        ViewBag.OwnerEmail = budget.Data.UserEmail;
        ViewBag.ReturnUserId = userId;
        var b = budget.Data;
        return View(new BudgetFormViewModel
        {
            Id = b.Id, CategoryId = b.CategoryId, Year = b.BudgetYear, Month = b.BudgetMonth, Amount = b.Amount,
            Categories = categories.Data!.Where(c => c.Type == TransactionType.Expense).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBudget(int id, string? userId, BudgetFormViewModel model)
    {
        model.Id = id;
        var budget = await Api.GetAsync<AdminBudgetResponse>($"api/admin/budgets/{id}");
        if (await FailAsync(budget) is { } f1) return f1;
        var categories = await OwnerCategoriesAsync(budget.Data!.UserId);
        if (await FailAsync(categories) is { } f2) return f2;
        model.Categories = categories.Data!.Where(c => c.Type == TransactionType.Expense).ToList();
        ViewBag.OwnerEmail = budget.Data.UserEmail;
        ViewBag.ReturnUserId = userId;

        if (!ModelState.IsValid) return View(model);

        var body = new BudgetRequest(model.CategoryId!.Value, model.Year!.Value, model.Month!.Value, model.Amount!.Value);
        var result = await Api.PutAsync<AdminBudgetResponse>($"api/admin/budgets/{id}", body);
        if (result.Unauthorized) return await SignOutAsync();
        if (!result.Ok)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        TempData["Success"] = $"Budget of {budget.Data.UserEmail} updated.";
        return RedirectToAction(nameof(Budgets), new { userId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBudget(int id, string? userId)
    {
        var result = await Api.DeleteAsync($"api/admin/budgets/{id}");
        if (result.Unauthorized) return await SignOutAsync();
        TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "Budget deleted." : result.Errors.FirstOrDefault();
        return RedirectToAction(nameof(Budgets), new { userId });
    }

    // ---------- edit / delete a user's category ----------

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id)
    {
        var category = await Api.GetAsync<AdminCategoryResponse>($"api/admin/categories/{id}");
        if (await FailAsync(category) is { } failure) return failure;
        if (category.Data!.IsDefault) return NotFound();   // shared defaults are read-only

        ViewBag.OwnerEmail = category.Data.OwnerEmail;
        return View(new CategoryFormViewModel { Id = id, Name = category.Data.Name, Type = category.Data.Type });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, CategoryFormViewModel model)
    {
        model.Id = id;
        var category = await Api.GetAsync<AdminCategoryResponse>($"api/admin/categories/{id}");
        if (await FailAsync(category) is { } failure) return failure;
        ViewBag.OwnerEmail = category.Data!.OwnerEmail;
        if (!ModelState.IsValid) return View(model);

        var result = await Api.PutAsync<AdminCategoryResponse>($"api/admin/categories/{id}",
            new CategoryRequest(model.Name.Trim(), model.Type!.Value));
        if (result.Unauthorized) return await SignOutAsync();
        if (!result.Ok)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        TempData["Success"] = $"Category of {category.Data.OwnerEmail} updated.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var result = await Api.DeleteAsync($"api/admin/categories/{id}");
        if (result.Unauthorized) return await SignOutAsync();
        TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "Category deleted." : result.Errors.FirstOrDefault();
        return RedirectToAction(nameof(Categories));
    }

    private Task<ApiResult<List<CategoryResponse>>> OwnerCategoriesAsync(string ownerUserId) =>
        Api.GetAsync<List<CategoryResponse>>($"api/admin/users/{Uri.EscapeDataString(ownerUserId)}/categories");
}

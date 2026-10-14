using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBMS.Application.Admin;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

/// <summary>Admin-only pages. The role check here hides the UI; the API enforces it on every call.</summary>
[Authorize(Roles = "Admin")]
public class AdminController(ApiClient api) : AppController(api)
{
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetAsync<List<AdminUserResponse>>("api/admin/users");
        if (await FailAsync(result) is { } failure) return failure;
        return View(result.Data!);
    }

    public async Task<IActionResult> Transactions(string? userId)
    {
        var result = await Api.GetAsync<List<AdminTransactionResponse>>(
            "api/admin/transactions" + (userId is null ? "" : $"?userId={Uri.EscapeDataString(userId)}"));
        if (await FailAsync(result) is { } failure) return failure;
        return View(result.Data!);
    }

    public async Task<IActionResult> Budgets(string? userId)
    {
        var result = await Api.GetAsync<List<AdminBudgetResponse>>(
            "api/admin/budgets" + (userId is null ? "" : $"?userId={Uri.EscapeDataString(userId)}"));
        if (await FailAsync(result) is { } failure) return failure;
        return View(result.Data!);
    }

    public async Task<IActionResult> Categories()
    {
        var result = await Api.GetAsync<List<AdminCategoryResponse>>("api/admin/categories");
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
}

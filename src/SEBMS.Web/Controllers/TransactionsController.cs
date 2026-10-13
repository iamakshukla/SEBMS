using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBMS.Application.Categories;
using SEBMS.Application.Transactions;
using SEBMS.Domain.Enums;
using SEBMS.Web.Models;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

[Authorize]
public class TransactionsController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, int? categoryId, TransactionType? type)
    {
        var query = new List<string>();
        if (from is not null) query.Add($"from={from:yyyy-MM-dd}");
        if (to is not null) query.Add($"to={to:yyyy-MM-dd}");
        if (categoryId is not null) query.Add($"categoryId={categoryId}");
        if (type is not null) query.Add($"type={type}");

        var items = await api.GetAsync<List<TransactionResponse>>("api/transactions" + (query.Count > 0 ? "?" + string.Join("&", query) : ""));
        var categories = await api.GetAsync<List<CategoryResponse>>("api/categories");
        if (await HandleFailureAsync(items, categories) is { } failure) return failure;

        return View(new TransactionListViewModel
        {
            From = from, To = to, CategoryId = categoryId, Type = type,
            Categories = categories.Data!, Items = items.Data!
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var categories = await api.GetAsync<List<CategoryResponse>>("api/categories");
        if (await HandleFailureAsync(categories) is { } failure) return failure;
        return View("Form", new TransactionFormViewModel
        {
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            Categories = categories.Data!
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(TransactionFormViewModel model) => SaveAsync(model);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var tx = await api.GetAsync<TransactionResponse>($"api/transactions/{id}");
        var categories = await api.GetAsync<List<CategoryResponse>>("api/categories");
        if (tx.Status == System.Net.HttpStatusCode.NotFound) return NotFound();
        if (await HandleFailureAsync(tx, categories) is { } failure) return failure;

        var t = tx.Data!;
        return View("Form", new TransactionFormViewModel
        {
            Id = t.Id, CategoryId = t.CategoryId, Amount = t.Amount,
            TransactionDate = t.TransactionDate, Note = t.Note, Categories = categories.Data!
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, TransactionFormViewModel model)
    {
        model.Id = id;
        return SaveAsync(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await api.DeleteAsync($"api/transactions/{id}");
        if (result.Unauthorized) return await SignOutAsync();
        TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "Transaction deleted." : result.Errors.FirstOrDefault();
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(TransactionFormViewModel model)
    {
        var categories = await api.GetAsync<List<CategoryResponse>>("api/categories");
        if (await HandleFailureAsync(categories) is { } failure) return failure;
        model.Categories = categories.Data!;

        // The category decides the transaction type, so the two can never disagree.
        var category = model.Categories.FirstOrDefault(c => c.Id == model.CategoryId);
        if (model.CategoryId is not null && category is null)
            ModelState.AddModelError(nameof(model.CategoryId), "Choose a valid category.");
        if (!ModelState.IsValid) return View("Form", model);

        var body = new TransactionRequest(category!.Id, category.Type, model.Amount!.Value,
            model.TransactionDate!.Value, string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim());
        var result = model.Id is null
            ? await api.PostAsync<TransactionResponse>("api/transactions", body)
            : await api.PutAsync<TransactionResponse>($"api/transactions/{model.Id}", body);

        if (result.Unauthorized) return await SignOutAsync();
        if (!result.Ok)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e);
            return View("Form", model);
        }

        TempData["Success"] = model.Id is null ? "Transaction added." : "Transaction updated.";
        return RedirectToAction(nameof(Index));
    }

    // Returns a redirect (expired token) or error page when any call failed; null when all succeeded.
    private async Task<IActionResult?> HandleFailureAsync(params object[] results)
    {
        foreach (var r in results)
        {
            var (ok, unauthorized, errors) = r switch
            {
                ApiResult<List<TransactionResponse>> x => (x.Ok, x.Unauthorized, x.Errors),
                ApiResult<List<CategoryResponse>> x => (x.Ok, x.Unauthorized, x.Errors),
                ApiResult<TransactionResponse> x => (x.Ok, x.Unauthorized, x.Errors),
                _ => (true, false, (IReadOnlyList<string>)[])
            };
            if (unauthorized) return await SignOutAsync();
            if (!ok)
            {
                ViewBag.Error = errors.FirstOrDefault() ?? "Request failed.";
                return View("~/Views/Dashboard/Unavailable.cshtml");
            }
        }
        return null;
    }

    private async Task<IActionResult> SignOutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }
}

using Microsoft.AspNetCore.Mvc;
using SEBMS.Application.Categories;
using SEBMS.Application.Recurring;
using SEBMS.Web.Models;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

public class RecurringController(ApiClient api) : AppController(api)
{
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetAsync<List<RecurringResponse>>("api/recurring");
        if (await FailAsync(result) is { } failure) return failure;
        return View(result.Data!);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var categories = await Api.GetAsync<List<CategoryResponse>>("api/categories");
        if (await FailAsync(categories) is { } failure) return failure;
        return View("Form", new RecurringFormViewModel
        {
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            Categories = categories.Data!
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(RecurringFormViewModel model) => SaveAsync(model);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var rule = await Api.GetAsync<RecurringResponse>($"api/recurring/{id}");
        var categories = await Api.GetAsync<List<CategoryResponse>>("api/categories");
        if (await FailAsync(rule) is { } f1) return f1;
        if (await FailAsync(categories) is { } f2) return f2;

        var r = rule.Data!;
        return View("Form", new RecurringFormViewModel
        {
            Id = r.Id, CategoryId = r.CategoryId, Amount = r.Amount, Frequency = r.Frequency,
            StartDate = r.StartDate, EndDate = r.EndDate, Note = r.Note, IsActive = r.IsActive,
            Categories = categories.Data!
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, RecurringFormViewModel model)
    {
        model.Id = id;
        return SaveAsync(model);
    }

    /// <summary>Pauses an active rule or resumes a paused one.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var rule = await Api.GetAsync<RecurringResponse>($"api/recurring/{id}");
        if (rule.Unauthorized) return await SignOutAsync();
        if (!rule.Ok) { TempData["Error"] = rule.Errors.FirstOrDefault(); return RedirectToAction(nameof(Index)); }

        var r = rule.Data!;
        var body = new RecurringRequest(r.CategoryId, r.Type, r.Amount, r.Frequency, r.StartDate, r.EndDate, r.Note,
            !r.IsActive, r.CurrencyCode);
        var result = await Api.PutAsync<RecurringResponse>($"api/recurring/{id}", body);
        if (result.Unauthorized) return await SignOutAsync();
        TempData[result.Ok ? "Success" : "Error"] = result.Ok
            ? (r.IsActive ? "Recurring transaction paused." : "Recurring transaction resumed.")
            : result.Errors.FirstOrDefault();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Api.DeleteAsync($"api/recurring/{id}");
        if (result.Unauthorized) return await SignOutAsync();
        TempData[result.Ok ? "Success" : "Error"] = result.Ok
            ? "Recurring transaction deleted. Transactions it already created were kept."
            : result.Errors.FirstOrDefault();
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(RecurringFormViewModel model)
    {
        var categories = await Api.GetAsync<List<CategoryResponse>>("api/categories");
        if (await FailAsync(categories) is { } failure) return failure;
        model.Categories = categories.Data!;

        // The category decides the type, so the two can never disagree.
        var category = model.Categories.FirstOrDefault(c => c.Id == model.CategoryId);
        if (model.CategoryId is not null && category is null)
            ModelState.AddModelError(nameof(model.CategoryId), "Choose a valid category.");
        if (!ModelState.IsValid) return View("Form", model);

        var body = new RecurringRequest(category!.Id, category.Type, model.Amount!.Value, model.Frequency!.Value,
            model.StartDate!.Value, model.EndDate, string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
            model.IsActive);
        var result = model.Id is null
            ? await Api.PostAsync<RecurringResponse>("api/recurring", body)
            : await Api.PutAsync<RecurringResponse>($"api/recurring/{model.Id}", body);

        if (result.Unauthorized) return await SignOutAsync();
        if (!result.Ok)
        {
            AddErrors(result.Errors);
            return View("Form", model);
        }

        TempData["Success"] = model.Id is null ? "Recurring transaction added." : "Recurring transaction updated.";
        return RedirectToAction(nameof(Index));
    }
}

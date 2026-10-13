using Microsoft.AspNetCore.Mvc;
using SEBMS.Application.Categories;
using SEBMS.Web.Models;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

public class CategoriesController(ApiClient api) : AppController(api)
{
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetAsync<List<CategoryResponse>>("api/categories");
        if (await FailAsync(result) is { } failure) return failure;
        return View(result.Data!);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new CategoryFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(CategoryFormViewModel model) => SaveAsync(model);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await Api.GetAsync<CategoryResponse>($"api/categories/{id}");
        if (await FailAsync(result) is { } failure) return failure;
        if (result.Data!.IsDefault) return NotFound();   // defaults are read-only
        return View("Form", new CategoryFormViewModel { Id = id, Name = result.Data.Name, Type = result.Data.Type });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, CategoryFormViewModel model)
    {
        model.Id = id;
        return SaveAsync(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Api.DeleteAsync($"api/categories/{id}");
        if (result.Unauthorized) return await SignOutAsync();
        TempData[result.Ok ? "Success" : "Error"] = result.Ok ? "Category deleted." : result.Errors.FirstOrDefault();
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var body = new CategoryRequest(model.Name.Trim(), model.Type!.Value);
        var result = model.Id is null
            ? await Api.PostAsync<CategoryResponse>("api/categories", body)
            : await Api.PutAsync<CategoryResponse>($"api/categories/{model.Id}", body);

        if (result.Unauthorized) return await SignOutAsync();
        if (!result.Ok)
        {
            AddErrors(result.Errors);
            return View("Form", model);
        }

        TempData["Success"] = model.Id is null ? "Category added." : "Category updated.";
        return RedirectToAction(nameof(Index));
    }
}

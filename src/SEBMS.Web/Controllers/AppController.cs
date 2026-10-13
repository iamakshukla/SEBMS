using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

/// <summary>Base for signed-in pages that call the API.</summary>
[Authorize]
public abstract class AppController(ApiClient api) : Controller
{
    protected ApiClient Api => api;

    /// <summary>
    /// Handles a failed API call: expired token -> sign out and redirect to login, 404 -> NotFound,
    /// anything else -> error page. Returns null when the call succeeded.
    /// </summary>
    protected async Task<IActionResult?> FailAsync<T>(ApiResult<T> result)
    {
        if (result.Ok) return null;
        if (result.Unauthorized) return await SignOutAsync();
        if (result.Status == System.Net.HttpStatusCode.NotFound) return NotFound();
        ViewBag.Error = result.Errors.FirstOrDefault() ?? "Request failed.";
        return View("~/Views/Dashboard/Unavailable.cshtml");
    }

    protected async Task<IActionResult> SignOutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }

    protected void AddErrors(IEnumerable<string> errors)
    {
        foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBMS.Application.Auth;
using SEBMS.Web.Models;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

public class AccountController(ApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await api.PostAsync<AuthResponse>("api/auth/login", new LoginRequest(model.Email, model.Password));
        if (!result.Ok || result.Data is null)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        await SignInAsync(result.Data);
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await api.PostAsync<AuthResponse>("api/auth/register",
            new RegisterRequest(model.Email, model.Password, model.DisplayName));
        if (!result.Ok || result.Data is null)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        await SignInAsync(result.Data);
        return RedirectToAction("Index", "Dashboard");
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private Task SignInAsync(AuthResponse auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(auth.DisplayName) ? auth.Email : auth.DisplayName),
            new(ClaimTypes.Email, auth.Email),
            new(ApiClient.TokenClaim, auth.Token)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = new DateTimeOffset(DateTime.SpecifyKind(auth.ExpiresAt, DateTimeKind.Utc))
        });
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
    }
}

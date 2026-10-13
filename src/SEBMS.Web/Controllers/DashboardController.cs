using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBMS.Application.Budgets;
using SEBMS.Application.Reports;
using SEBMS.Web.Models;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

[Authorize]
public class DashboardController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index(short? year, byte? month)
    {
        var today = DateTime.Today;
        var y = year is >= 2000 and <= 2100 ? year.Value : (short)today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : (byte)today.Month;

        var summary = await api.GetAsync<SummaryResponse>($"api/reports/summary?year={y}&month={m}");
        var spending = await api.GetAsync<CategoryBreakdownResponse>($"api/reports/by-category?year={y}&month={m}");
        var budgets = await api.GetAsync<List<BudgetResponse>>($"api/budgets?year={y}&month={m}");
        var trend = await api.GetAsync<MonthlyTrendResponse>($"api/reports/monthly-trend?year={y}");

        // Token rejected (expired or signing key changed): drop the stale cookie and ask for a fresh login.
        if (summary.Unauthorized || spending.Unauthorized || budgets.Unauthorized || trend.Unauthorized)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        if (summary.Data is null || spending.Data is null || budgets.Data is null || trend.Data is null)
        {
            var errors = new[] { summary.Errors, spending.Errors, budgets.Errors, trend.Errors };
            ViewBag.Error = errors.FirstOrDefault(e => e.Count > 0)?.FirstOrDefault() ?? "Could not load the dashboard.";
            return View("Unavailable");
        }

        return View(new DashboardViewModel
        {
            Year = y, Month = m,
            Summary = summary.Data, Spending = spending.Data, Budgets = budgets.Data, Trend = trend.Data
        });
    }
}

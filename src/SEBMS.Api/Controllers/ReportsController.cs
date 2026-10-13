using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SEBMS.Application.Reports;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using SEBMS.Infrastructure.Persistence;

namespace SEBMS.Api.Controllers;

public class ReportsController(AppDbContext db) : ApiControllerBase
{
    private IQueryable<Transaction> Mine => db.Transactions.Where(t => t.UserId == UserId && !t.IsDeleted);

    private IQueryable<Transaction> InMonth(short year, byte month)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        return Mine.Where(t => t.TransactionDate >= start && t.TransactionDate < end);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SummaryResponse>> Summary(
        [Range(2000, 2100)] short year, [Range(1, 12)] byte month)
    {
        var rows = await InMonth(year, month)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync();

        var income = rows.Where(r => r.Type == TransactionType.Income).Sum(r => r.Total);
        var expense = rows.Where(r => r.Type == TransactionType.Expense).Sum(r => r.Total);
        return new SummaryResponse(year, month, income, expense, income - expense, rows.Sum(r => r.Count));
    }

    [HttpGet("by-category")]
    public async Task<ActionResult<CategoryBreakdownResponse>> ByCategory(
        [Range(2000, 2100)] short year, [Range(1, 12)] byte month, TransactionType type = TransactionType.Expense)
    {
        var rows = await InMonth(year, month)
            .Where(t => t.Type == type)
            .GroupBy(t => new { t.CategoryId, t.Category!.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(r => r.Total)
            .ToListAsync();

        var total = rows.Sum(r => r.Total);
        var items = rows
            .Select(r => new CategoryBreakdownItem(r.CategoryId, r.Name, r.Total,
                total == 0 ? 0 : Math.Round(r.Total / total * 100, 2)))
            .ToList();
        return new CategoryBreakdownResponse(year, month, type, total, items);
    }

    [HttpGet("monthly-trend")]
    public async Task<ActionResult<MonthlyTrendResponse>> MonthlyTrend([Range(2000, 2100)] short year)
    {
        var start = new DateOnly(year, 1, 1);
        var end = start.AddYears(1);
        var rows = await Mine
            .Where(t => t.TransactionDate >= start && t.TransactionDate < end)
            .GroupBy(t => new { t.TransactionDate.Month, t.Type })
            .Select(g => new { g.Key.Month, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync();

        // Always return all 12 months so charts get a complete series.
        var months = Enumerable.Range(1, 12).Select(m =>
        {
            var income = rows.Where(r => r.Month == m && r.Type == TransactionType.Income).Sum(r => r.Total);
            var expense = rows.Where(r => r.Month == m && r.Type == TransactionType.Expense).Sum(r => r.Total);
            return new MonthlyTrendItem((byte)m, income, expense, income - expense);
        }).ToList();
        return new MonthlyTrendResponse(year, months);
    }
}

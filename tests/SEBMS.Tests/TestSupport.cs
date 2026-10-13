using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using SEBMS.Infrastructure.Persistence;

namespace SEBMS.Tests;

/// <summary>Seeded default category ids (see CategoryConfiguration).</summary>
public static class Seed
{
    public const int Food = 1;        // Expense
    public const int Transport = 2;   // Expense
    public const int Salary = 10;     // Income
    public const int Business = 11;   // Income
}

public static class TestSupport
{
    public const string Alice = "user-alice";
    public const string Bob = "user-bob";

    /// <summary>A fresh in-memory database per call, including the seeded default categories.</summary>
    public static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>Makes the controller act as the given signed-in user.</summary>
    public static T As<T>(this T controller, string userId) where T : ControllerBase
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    /// <summary>Unwraps ActionResult&lt;T&gt;: the value on success, otherwise the result object.</summary>
    public static object? Unwrap<T>(this ActionResult<T> result) => result.Result ?? (object?)result.Value;

    public static T Value<T>(this ActionResult<T> result) =>
        result.Value ?? (T)((ObjectResult)result.Result!).Value!;

    public static Transaction Tx(string userId, int categoryId, decimal amount, string date,
        TransactionType type = TransactionType.Expense, bool deleted = false) => new()
    {
        UserId = userId, CategoryId = categoryId, Type = type, Amount = amount,
        TransactionDate = DateOnly.Parse(date), IsDeleted = deleted
    };
}

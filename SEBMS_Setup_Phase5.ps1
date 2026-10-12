# SEBMS Phase 5 - creates entity, DbContext and configuration files, wires Program.cs,
# installs EF tooling and sets the connection string in user-secrets.
# Run from the ROOT of the repo (D:\Akash\SEBMS) in PowerShell. Safe to run once.
$ErrorActionPreference = "Stop"

if (-not (Test-Path "SEBMS.sln")) { throw "Run this from the folder that contains SEBMS.sln" }

function Write-Source([string]$Path, [string]$Content) {
    $dir = Split-Path $Path -Parent
    New-Item -ItemType Directory -Force $dir | Out-Null
    [System.IO.File]::WriteAllText((Join-Path (Get-Location) $Path), $Content.TrimStart(), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "created $Path"
}

# ------------------------------------------------------------------ Domain
Write-Source "src/SEBMS.Domain/Enums/TransactionType.cs" @'
namespace SEBMS.Domain.Enums;

public enum TransactionType
{
    Income = 1,
    Expense = 2
}
'@

Write-Source "src/SEBMS.Domain/Entities/Category.cs" @'
using SEBMS.Domain.Enums;

namespace SEBMS.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string? UserId { get; set; }          // null for shared default categories
    public string Name { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public bool IsDefault { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
'@

Write-Source "src/SEBMS.Domain/Entities/Transaction.cs" @'
using SEBMS.Domain.Enums;

namespace SEBMS.Domain.Entities;

public class Transaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? Note { get; set; }
    public string CurrencyCode { get; set; } = "INR";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Category? Category { get; set; }
}
'@

Write-Source "src/SEBMS.Domain/Entities/Budget.cs" @'
namespace SEBMS.Domain.Entities;

public class Budget
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public short BudgetYear { get; set; }
    public byte BudgetMonth { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Category? Category { get; set; }
}
'@

# ---------------------------------------------------------- Infrastructure
Write-Source "src/SEBMS.Infrastructure/Identity/ApplicationUser.cs" @'
using Microsoft.AspNetCore.Identity;

namespace SEBMS.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
'@

Write-Source "src/SEBMS.Infrastructure/Persistence/AppDbContext.cs" @'
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SEBMS.Domain.Entities;
using SEBMS.Infrastructure.Identity;

namespace SEBMS.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);   // creates the Identity tables
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
'@

Write-Source "src/SEBMS.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs" @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using SEBMS.Infrastructure.Identity;

namespace SEBMS.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories", t =>
        {
            t.HasCheckConstraint("CK_Categories_Type", "[Type] IN ('Income','Expense')");
            t.HasCheckConstraint("CK_Categories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint("CK_Categories_DefaultOwner",
                "([IsDefault] = 1 AND [UserId] IS NULL) OR ([IsDefault] = 0 AND [UserId] IS NOT NULL)");
        });

        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.Name).IsRequired().HasMaxLength(50);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(7).IsUnicode(false);

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.UserId, x.Name, x.Type }).IsUnique()
            .HasDatabaseName("UX_Categories_User_Name_Type");

        b.HasData(
            new Category { Id = 1,  Name = "Food and Dining",  Type = TransactionType.Expense, IsDefault = true },
            new Category { Id = 2,  Name = "Transport",        Type = TransactionType.Expense, IsDefault = true },
            new Category { Id = 3,  Name = "Rent and Housing", Type = TransactionType.Expense, IsDefault = true },
            new Category { Id = 4,  Name = "Utilities",        Type = TransactionType.Expense, IsDefault = true },
            new Category { Id = 5,  Name = "Shopping",         Type = TransactionType.Expense, IsDefault = true },
            new Category { Id = 6,  Name = "Health",           Type = TransactionType.Expense, IsDefault = true },
            new Category { Id = 7,  Name = "Education",        Type = TransactionType.Expense, IsDefault = true },
            new Category { Id = 8,  Name = "Entertainment",    Type = TransactionType.Expense, IsDefault = true },
            new Category { Id = 9,  Name = "Other Expense",    Type = TransactionType.Expense, IsDefault = true },
            new Category { Id = 10, Name = "Salary",           Type = TransactionType.Income,  IsDefault = true },
            new Category { Id = 11, Name = "Business",         Type = TransactionType.Income,  IsDefault = true },
            new Category { Id = 12, Name = "Gift",             Type = TransactionType.Income,  IsDefault = true },
            new Category { Id = 13, Name = "Other Income",     Type = TransactionType.Income,  IsDefault = true });
    }
}
'@

Write-Source "src/SEBMS.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs" @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEBMS.Domain.Entities;
using SEBMS.Infrastructure.Identity;

namespace SEBMS.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable("Transactions", t =>
        {
            t.HasCheckConstraint("CK_Transactions_Type", "[Type] IN ('Income','Expense')");
            t.HasCheckConstraint("CK_Transactions_Amount", "[Amount] > 0 AND [Amount] <= 9999999999");
            t.HasCheckConstraint("CK_Transactions_Currency",
                "[CurrencyCode] COLLATE Latin1_General_BIN LIKE '[A-Z][A-Z][A-Z]'");
        });

        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(7).IsUnicode(false);
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.CurrencyCode).IsRequired().HasColumnType("char(3)").HasDefaultValue("INR");
        b.Property(x => x.IsDeleted).HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany(c => c.Transactions).HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);   // a category with transactions cannot be deleted

        b.HasIndex(x => new { x.UserId, x.TransactionDate })
            .IsDescending(false, true)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Transactions_User_Date");

        b.HasIndex(x => new { x.UserId, x.CategoryId, x.TransactionDate })
            .IncludeProperties(x => new { x.Amount, x.Type })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Transactions_User_Category_Date");
    }
}
'@

Write-Source "src/SEBMS.Infrastructure/Persistence/Configurations/BudgetConfiguration.cs" @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEBMS.Domain.Entities;
using SEBMS.Infrastructure.Identity;

namespace SEBMS.Infrastructure.Persistence.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> b)
    {
        b.ToTable("Budgets", t =>
        {
            t.HasCheckConstraint("CK_Budgets_Month", "[BudgetMonth] BETWEEN 1 AND 12");
            t.HasCheckConstraint("CK_Budgets_Amount", "[Amount] > 0");
        });

        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany(c => c.Budgets).HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);    // deleting a category deletes its budgets

        b.HasIndex(x => new { x.UserId, x.BudgetYear, x.BudgetMonth })
            .HasDatabaseName("IX_Budgets_User_Year_Month");
        b.HasIndex(x => new { x.UserId, x.CategoryId, x.BudgetYear, x.BudgetMonth }).IsUnique()
            .HasDatabaseName("UX_Budgets_User_Category_Year_Month");
    }
}
'@

# ----------------------------------------------------- Packages and tooling
dotnet add src/SEBMS.Api package Microsoft.EntityFrameworkCore.Design

$ErrorActionPreference = "Continue"
dotnet tool update --global dotnet-ef
if ($LASTEXITCODE -ne 0) { dotnet tool install --global dotnet-ef }
$ErrorActionPreference = "Stop"

# ------------------------------------------------- Connection string (secret)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\SQLEXPRESS;Database=SEBMS;Trusted_Connection=True;TrustServerCertificate=True;" --project src/SEBMS.Api

# ------------------------------------------------------ Patch Program.cs
$program = "src/SEBMS.Api/Program.cs"
$text = [System.IO.File]::ReadAllText((Join-Path (Get-Location) $program))
if ($text -match "AddDbContext<AppDbContext>") {
    Write-Host "Program.cs already patched - skipped"
}
else {
    $anchor = "var builder = WebApplication.CreateBuilder(args);"
    if ($text -notlike "*$anchor*") { throw "Could not find '$anchor' in Program.cs - add the lines from the doc by hand." }
    $wiring = $anchor + "`r`n`r`n" +
        "builder.Services.AddDbContext<AppDbContext>(options =>`r`n" +
        "    options.UseSqlServer(builder.Configuration.GetConnectionString(`"DefaultConnection`")));`r`n`r`n" +
        "builder.Services.AddIdentityCore<ApplicationUser>()`r`n" +
        "    .AddEntityFrameworkStores<AppDbContext>();"
    $text = $text.Replace($anchor, $wiring)
    $usings = "using Microsoft.EntityFrameworkCore;`r`nusing SEBMS.Infrastructure.Identity;`r`nusing SEBMS.Infrastructure.Persistence;`r`n"
    $text = $usings + $text
    [System.IO.File]::WriteAllText((Join-Path (Get-Location) $program), $text, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "patched $program"
}

Write-Host ""
Write-Host "Done. Next: dotnet build SEBMS.sln"

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
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
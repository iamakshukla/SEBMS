using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEBMS.Domain.Entities;
using SEBMS.Infrastructure.Identity;

namespace SEBMS.Infrastructure.Persistence.Configurations;

public class RecurringTransactionConfiguration : IEntityTypeConfiguration<RecurringTransaction>
{
    public void Configure(EntityTypeBuilder<RecurringTransaction> b)
    {
        b.ToTable("RecurringTransactions", t =>
        {
            t.HasCheckConstraint("CK_RecurringTransactions_Type", "[Type] IN ('Income','Expense')");
            t.HasCheckConstraint("CK_RecurringTransactions_Amount", "[Amount] > 0 AND [Amount] <= 9999999999");
            t.HasCheckConstraint("CK_RecurringTransactions_Frequency", "[Frequency] IN ('Weekly','Monthly','Yearly')");
            t.HasCheckConstraint("CK_RecurringTransactions_Dates", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
        });

        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(7).IsUnicode(false);
        b.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(7).IsUnicode(false);
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.CurrencyCode).IsRequired().HasColumnType("char(3)").HasDefaultValue("INR");
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.GeneratedCount).IsConcurrencyToken();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Ignore(x => x.NextDueDate);
        b.Ignore(x => x.IsFinished);

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.UserId, x.IsActive }).HasDatabaseName("IX_RecurringTransactions_User_Active");
    }
}

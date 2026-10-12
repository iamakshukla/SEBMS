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
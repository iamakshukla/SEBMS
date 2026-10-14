using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurringTransactionId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(7)", unicode: false, maxLength: 7, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "INR"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Frequency = table.Column<string>(type: "varchar(7)", unicode: false, maxLength: 7, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    GeneratedCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTransactions", x => x.Id);
                    table.CheckConstraint("CK_RecurringTransactions_Amount", "[Amount] > 0 AND [Amount] <= 9999999999");
                    table.CheckConstraint("CK_RecurringTransactions_Dates", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_RecurringTransactions_Frequency", "[Frequency] IN ('Weekly','Monthly','Yearly')");
                    table.CheckConstraint("CK_RecurringTransactions_Type", "[Type] IN ('Income','Expense')");
                    table.ForeignKey(
                        name: "FK_RecurringTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringTransactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Recurring",
                table: "Transactions",
                column: "RecurringTransactionId",
                filter: "[RecurringTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_CategoryId",
                table: "RecurringTransactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_User_Active",
                table: "RecurringTransactions",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_RecurringTransactions_RecurringTransactionId",
                table: "Transactions",
                column: "RecurringTransactionId",
                principalTable: "RecurringTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_RecurringTransactions_RecurringTransactionId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Recurring",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RecurringTransactionId",
                table: "Transactions");
        }
    }
}

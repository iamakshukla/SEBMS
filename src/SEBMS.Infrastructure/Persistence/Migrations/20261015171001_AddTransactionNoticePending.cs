using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionNoticePending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NoticePending",
                table: "Transactions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoticePending",
                table: "Transactions");
        }
    }
}

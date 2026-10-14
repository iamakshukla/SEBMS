using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AdminUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AdminEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    EntityType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TargetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TargetEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAt",
                table: "AuditLogs",
                column: "OccurredAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Target",
                table: "AuditLogs",
                columns: new[] { "TargetUserId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamificationService.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectProgressSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPurchasable",
                table: "shop_items",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "project_progress_snapshots",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    progress_percent = table.Column<double>(type: "double precision", nullable: false),
                    total_weight = table.Column<double>(type: "double precision", nullable: false),
                    completed_weight = table.Column<double>(type: "double precision", nullable: false),
                    total_tasks = table.Column<int>(type: "integer", nullable: false),
                    completed_tasks = table.Column<int>(type: "integer", nullable: false),
                    total_subtasks = table.Column<int>(type: "integer", nullable: false),
                    completed_subtasks = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_progress_snapshots", x => x.project_id);
                });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000001"),
                column: "IsPurchasable",
                value: true);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000002"),
                column: "IsPurchasable",
                value: true);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000003"),
                column: "IsPurchasable",
                value: true);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000004"),
                column: "IsPurchasable",
                value: true);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000005"),
                column: "IsPurchasable",
                value: true);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000006"),
                column: "IsPurchasable",
                value: true);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000007"),
                column: "IsPurchasable",
                value: true);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000008"),
                column: "IsPurchasable",
                value: false);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000009"),
                column: "IsPurchasable",
                value: false);

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000010"),
                column: "IsPurchasable",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_progress_snapshots");

            migrationBuilder.DropColumn(
                name: "IsPurchasable",
                table: "shop_items");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamificationService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGamificationSeriesAndSkillTreeConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "skill_tree_nodes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000002"),
                columns: new[] { "Description", "Requirement" },
                values: new object[] { "Add 1 comment", "{\"type\":\"add_comments\",\"count\":1}" });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000007"),
                columns: new[] { "Description", "Name", "Price" },
                values: new object[] { "Protects your series for 1 day", "Series Freeze", 500 });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000008"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Annual rank award for the global top performer", "Gold Rank" });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000009"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Annual rank award for the global second place", "Silver Rank" });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000010"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Annual rank award for the global third place", "Bronze Rank" });

            migrationBuilder.UpdateData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000001"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000002"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000003"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000004"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000005"),
                columns: new[] { "Description", "IsActive" },
                values: new object[] { "Final reward for users leading high-output delivery.", true });

            migrationBuilder.UpdateData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000006"),
                columns: new[] { "Description", "IsActive" },
                values: new object[] { "Final reward centered on team energy and shared gains.", true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "skill_tree_nodes");

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000002"),
                columns: new[] { "Description", "Requirement" },
                values: new object[] { "Add 5 comments", "{\"type\":\"add_comments\",\"count\":5}" });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000007"),
                columns: new[] { "Description", "Name", "Price" },
                values: new object[] { "Protects your streak for 1 day", "Streak Freeze", 50 });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000008"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Annual award for the global rank #1 performer", "Gold Crown" });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000009"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Annual award for the global rank #2 performer", "Silver Crown" });

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000010"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Annual award for the global rank #3 performer", "Bronze Crown" });

            migrationBuilder.UpdateData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000005"),
                column: "Description",
                value: "Final specialization for users leading high-output delivery.");

            migrationBuilder.UpdateData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000006"),
                column: "Description",
                value: "Final specialization centered on team energy and rewards.");
        }
    }
}

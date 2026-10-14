using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GamificationService.Migrations
{
    /// <inheritdoc />
    public partial class InitialGamificationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "xp_transactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Applied");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "user_quests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindowKey",
                table: "user_quests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.InsertData(
                table: "shop_items",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "ImageUrl", "IsActive", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-0000-0000-000000000008"), "badges", new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Annual award for the global rank #1 performer", null, true, "Gold Crown", 0 },
                    { new Guid("a0000001-0000-0000-0000-000000000009"), "badges", new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Annual award for the global rank #2 performer", null, true, "Silver Crown", 0 },
                    { new Guid("a0000001-0000-0000-0000-000000000010"), "badges", new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Annual award for the global rank #3 performer", null, true, "Bronze Crown", 0 }
                });

            migrationBuilder.InsertData(
                table: "skill_tree_nodes",
                columns: new[] { "Id", "Branch", "CreatedAt", "Description", "ProjectId", "RewardJson", "Tier", "Title" },
                values: new object[,]
                {
                    { new Guid("c0000001-0000-0000-0000-000000000001"), "A", new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Sharpen delivery speed for your first specialization.", new Guid("00000000-0000-0000-0000-000000000000"), "{\"title\":\"Executor\",\"xpMultiplier\":1.05}", 1, "Execution Focus" },
                    { new Guid("c0000001-0000-0000-0000-000000000002"), "B", new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Boost collaborative impact and unlock support-oriented rewards.", new Guid("00000000-0000-0000-0000-000000000000"), "{\"title\":\"Supporter\",\"coinBonus\":10}", 1, "Team Support" },
                    { new Guid("c0000001-0000-0000-0000-000000000003"), "A", new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Turn consistent progress into stronger XP gains.", new Guid("00000000-0000-0000-0000-000000000000"), "{\"title\":\"Momentum Builder\",\"xpMultiplier\":1.1}", 2, "Momentum Builder" },
                    { new Guid("c0000001-0000-0000-0000-000000000004"), "B", new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Earn cosmetic recognition for guiding the project forward.", new Guid("00000000-0000-0000-0000-000000000000"), "{\"badge\":\"mentor-aura\"}", 2, "Mentor Aura" },
                    { new Guid("c0000001-0000-0000-0000-000000000005"), "A", new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Final specialization for users leading high-output delivery.", new Guid("00000000-0000-0000-0000-000000000000"), "{\"title\":\"Project Vanguard\",\"xpMultiplier\":1.15}", 3, "Project Vanguard" },
                    { new Guid("c0000001-0000-0000-0000-000000000006"), "B", new DateTime(2026, 3, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Final specialization centered on team energy and rewards.", new Guid("00000000-0000-0000-0000-000000000000"), "{\"badge\":\"culture-catalyst\",\"coinBonus\":20}", 3, "Culture Catalyst" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "skill_tree_nodes",
                keyColumn: "Id",
                keyValue: new Guid("c0000001-0000-0000-0000-000000000006"));

            migrationBuilder.DropColumn(
                name: "Status",
                table: "xp_transactions");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "user_quests");

            migrationBuilder.DropColumn(
                name: "WindowKey",
                table: "user_quests");

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(9185));

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 437, DateTimeKind.Utc).AddTicks(273));

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 437, DateTimeKind.Utc).AddTicks(276));

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 437, DateTimeKind.Utc).AddTicks(278));

            migrationBuilder.UpdateData(
                table: "quests",
                keyColumn: "Id",
                keyValue: new Guid("b0000001-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 437, DateTimeKind.Utc).AddTicks(284));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(1619));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2734));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2739));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2749));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2751));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2752));

            migrationBuilder.UpdateData(
                table: "shop_items",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2754));
        }
    }
}

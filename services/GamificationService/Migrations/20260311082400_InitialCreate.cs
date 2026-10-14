using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GamificationService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leaderboard_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Xp = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaderboard_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "quests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    XpReward = table.Column<int>(type: "integer", nullable: false),
                    CoinReward = table.Column<int>(type: "integer", nullable: false),
                    Requirement = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shop_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "skill_tree_nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Branch = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    RewardJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_tree_nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_global_xp",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalXp = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Coins = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    StreakDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastActiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FreezeCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_global_xp", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "user_project_xp",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Xp = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    TasksCompleted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_project_xp", x => new { x.UserId, x.ProjectId });
                });

            migrationBuilder.CreateTable(
                name: "xp_config",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeightSubtasks = table.Column<bool>(type: "boolean", nullable: false),
                    WeightPriority = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewRequired = table.Column<bool>(type: "boolean", nullable: false),
                    FormulaJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xp_config", x => x.ProjectId);
                });

            migrationBuilder.CreateTable(
                name: "xp_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    CoinsAmount = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xp_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_quests",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_quests", x => new { x.UserId, x.QuestId });
                    table.ForeignKey(
                        name: "FK_user_quests_quests_QuestId",
                        column: x => x.QuestId,
                        principalTable: "quests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_inventory",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_inventory", x => new { x.UserId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_user_inventory_shop_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "shop_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_skill_choices",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChosenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_skill_choices", x => new { x.UserId, x.ProjectId, x.Tier });
                    table.ForeignKey(
                        name: "FK_user_skill_choices_skill_tree_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "skill_tree_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "quests",
                columns: new[] { "Id", "CoinReward", "CreatedAt", "Description", "IsActive", "Requirement", "Title", "Type", "XpReward" },
                values: new object[,]
                {
                    { new Guid("b0000001-0000-0000-0000-000000000001"), 10, new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(9185), "Complete 3 tasks", true, "{\"type\":\"complete_tasks\",\"count\":3}", "Task Starter", "Daily", 15 },
                    { new Guid("b0000001-0000-0000-0000-000000000002"), 5, new DateTime(2026, 3, 11, 8, 23, 55, 437, DateTimeKind.Utc).AddTicks(273), "Add 5 comments", true, "{\"type\":\"add_comments\",\"count\":5}", "Commentator", "Daily", 10 },
                    { new Guid("b0000001-0000-0000-0000-000000000003"), 15, new DateTime(2026, 3, 11, 8, 23, 55, 437, DateTimeKind.Utc).AddTicks(276), "Complete 1 high-priority task", true, "{\"type\":\"complete_high_priority\",\"count\":1}", "Priority Hunter", "Daily", 20 },
                    { new Guid("b0000001-0000-0000-0000-000000000004"), 30, new DateTime(2026, 3, 11, 8, 23, 55, 437, DateTimeKind.Utc).AddTicks(278), "Complete 15 tasks this week", true, "{\"type\":\"complete_tasks\",\"count\":15}", "Workhorse", "Weekly", 50 },
                    { new Guid("b0000001-0000-0000-0000-000000000005"), 20, new DateTime(2026, 3, 11, 8, 23, 55, 437, DateTimeKind.Utc).AddTicks(284), "Maintain a 7-day streak", true, "{\"type\":\"maintain_streak\",\"count\":7}", "Streak Master", "Weekly", 30 }
                });

            migrationBuilder.InsertData(
                table: "shop_items",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "ImageUrl", "IsActive", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-0000-0000-000000000001"), "frames", new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(1619), "A prestigious gold avatar frame", null, true, "Gold Frame", 500 },
                    { new Guid("a0000001-0000-0000-0000-000000000002"), "frames", new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2734), "A vibrant neon avatar frame", null, true, "Neon Frame", 1000 },
                    { new Guid("a0000001-0000-0000-0000-000000000003"), "frames", new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2739), "An animated fire avatar frame", null, true, "Fire Frame", 2000 },
                    { new Guid("a0000001-0000-0000-0000-000000000004"), "themes", new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2749), "A calming forest color scheme", null, true, "Forest Theme", 800 },
                    { new Guid("a0000001-0000-0000-0000-000000000005"), "badges", new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2751), "Badge for early adopters", null, true, "Early Access Badge", 100 },
                    { new Guid("a0000001-0000-0000-0000-000000000006"), "badges", new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2752), "The ultimate PRO badge", null, true, "PRO Badge", 5000 },
                    { new Guid("a0000001-0000-0000-0000-000000000007"), "consumables", new DateTime(2026, 3, 11, 8, 23, 55, 436, DateTimeKind.Utc).AddTicks(2754), "Protects your streak for 1 day", null, true, "Streak Freeze", 50 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_leaderboard_snapshots_Type_Year",
                table: "leaderboard_snapshots",
                columns: new[] { "Type", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_skill_tree_nodes_ProjectId",
                table: "skill_tree_nodes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_user_inventory_ItemId",
                table: "user_inventory",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_user_project_xp_ProjectId",
                table: "user_project_xp",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_user_quests_QuestId",
                table: "user_quests",
                column: "QuestId");

            migrationBuilder.CreateIndex(
                name: "IX_user_quests_UserId",
                table: "user_quests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_skill_choices_NodeId",
                table: "user_skill_choices",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_xp_transactions_CreatedAt",
                table: "xp_transactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_xp_transactions_UserId",
                table: "xp_transactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_xp_transactions_UserId_ProjectId",
                table: "xp_transactions",
                columns: new[] { "UserId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leaderboard_snapshots");

            migrationBuilder.DropTable(
                name: "user_global_xp");

            migrationBuilder.DropTable(
                name: "user_inventory");

            migrationBuilder.DropTable(
                name: "user_project_xp");

            migrationBuilder.DropTable(
                name: "user_quests");

            migrationBuilder.DropTable(
                name: "user_skill_choices");

            migrationBuilder.DropTable(
                name: "xp_config");

            migrationBuilder.DropTable(
                name: "xp_transactions");

            migrationBuilder.DropTable(
                name: "shop_items");

            migrationBuilder.DropTable(
                name: "quests");

            migrationBuilder.DropTable(
                name: "skill_tree_nodes");
        }
    }
}

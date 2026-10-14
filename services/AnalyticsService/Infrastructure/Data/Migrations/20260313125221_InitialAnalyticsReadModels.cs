using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnalyticsService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAnalyticsReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_inbox_events",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceService = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    XpDelta = table.Column<long>(type: "bigint", nullable: true),
                    CoinsDelta = table.Column<long>(type: "bigint", nullable: true),
                    LevelAfterEvent = table.Column<int>(type: "integer", nullable: true),
                    IsProjectionSupported = table.Column<bool>(type: "boolean", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_inbox_events", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "project_analytics_summaries",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    TasksReopened = table.Column<int>(type: "integer", nullable: false),
                    SubtasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    CommentsAdded = table.Column<int>(type: "integer", nullable: false),
                    TotalXpDelta = table.Column<long>(type: "bigint", nullable: false),
                    TotalCoinsDelta = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_analytics_summaries", x => x.ProjectId);
                });

            migrationBuilder.CreateTable(
                name: "user_analytics_summaries",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    TasksReopened = table.Column<int>(type: "integer", nullable: false),
                    SubtasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    CommentsAdded = table.Column<int>(type: "integer", nullable: false),
                    TotalXpDelta = table.Column<long>(type: "bigint", nullable: false),
                    TotalCoinsDelta = table.Column<long>(type: "bigint", nullable: false),
                    CurrentLevel = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_analytics_summaries", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analytics_inbox_events_OccurredAtUtc",
                table: "analytics_inbox_events",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_analytics_inbox_events_SourceService_EventType",
                table: "analytics_inbox_events",
                columns: new[] { "SourceService", "EventType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_inbox_events");

            migrationBuilder.DropTable(
                name: "project_analytics_summaries");

            migrationBuilder.DropTable(
                name: "user_analytics_summaries");
        }
    }
}

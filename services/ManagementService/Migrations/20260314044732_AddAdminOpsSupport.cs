using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementService.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminOpsSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_restart_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetService = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OwnedStateUnitsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeploymentGeneration = table.Column<long>(type: "bigint", nullable: true),
                    DeploymentUid = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RolloutStatus = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DeploymentNamespace = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_restart_operations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_restart_operations_TargetService_RequestedAtUtc",
                table: "service_restart_operations",
                columns: new[] { "TargetService", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_restart_operations");
        }
    }
}

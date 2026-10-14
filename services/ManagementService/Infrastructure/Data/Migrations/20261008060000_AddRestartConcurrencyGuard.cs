using ManagementService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ManagementService.Infrastructure.Data.Migrations;

[DbContext(typeof(ManagementDbContext))]
[Migration("20261008060000_AddRestartConcurrencyGuard")]
public sealed class AddRestartConcurrencyGuard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_service_restart_operations_TargetService_active\" ON service_restart_operations (\"TargetService\") WHERE \"Status\" IN ('queued','triggered','running');");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_service_restart_operations_TargetService_active\";");
}

using System.Reflection;
using ManagementService.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ManagementService.Tests.Unit;

public sealed class MigrationGuardTests
{
    [Fact]
    public void AddProjectBoardStickerIdsMigration_ShouldGuardAgainstPreExistingColumn()
    {
        var migration = new AddProjectBoardStickerIds();

        var upBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        InvokeMigrationMethod(migration, "Up", upBuilder);

        var upSql = Assert.Single(upBuilder.Operations.OfType<SqlOperation>()).Sql;
        Assert.Contains("IF NOT EXISTS", upSql, StringComparison.Ordinal);
        Assert.Contains(@"""Projects""", upSql, StringComparison.Ordinal);
        Assert.Contains(@"""BoardStickerIds""", upSql, StringComparison.Ordinal);

        var downBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        InvokeMigrationMethod(migration, "Down", downBuilder);

        var downSql = Assert.Single(downBuilder.Operations.OfType<SqlOperation>()).Sql;
        Assert.Contains("IF EXISTS", downSql, StringComparison.Ordinal);
        Assert.Contains(@"""Projects""", downSql, StringComparison.Ordinal);
        Assert.Contains(@"""BoardStickerIds""", downSql, StringComparison.Ordinal);
    }

    private static void InvokeMigrationMethod(Migration migration, string methodName, MigrationBuilder migrationBuilder)
    {
        var method = migration.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(migration, [migrationBuilder]);
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLandingLocaleContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "landing_locale_contents",
                columns: table => new
                {
                    Locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_landing_locale_contents", x => x.Locale);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "landing_locale_contents");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBoardStickerIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Projects'
                          AND column_name = 'BoardStickerIds'
                    ) THEN
                        ALTER TABLE "Projects"
                        ADD COLUMN "BoardStickerIds" character varying(2000) NULL;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Projects'
                          AND column_name = 'BoardStickerIds'
                    ) THEN
                        ALTER TABLE "Projects"
                        DROP COLUMN "BoardStickerIds";
                    END IF;
                END
                $$;
                """);
        }
    }
}

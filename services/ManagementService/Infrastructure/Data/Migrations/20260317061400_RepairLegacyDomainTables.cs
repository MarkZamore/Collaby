using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairLegacyDomainTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Tasks"
                ADD COLUMN IF NOT EXISTS "StickerValues" character varying(4000) NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "Notifications" (
                    "Id" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "Type" integer NOT NULL,
                    "Title" character varying(300) NOT NULL,
                    "Message" character varying(1000) NOT NULL,
                    "IsRead" boolean NOT NULL,
                    "ReferenceId" uuid NULL,
                    "SecondaryReferenceId" uuid NULL,
                    "ActorId" uuid NULL,
                    "ActionStatus" character varying(50) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id")
                );

                CREATE INDEX IF NOT EXISTS "IX_Notifications_UserId_IsRead"
                    ON "Notifications" ("UserId", "IsRead");

                CREATE INDEX IF NOT EXISTS "IX_Notifications_CreatedAt"
                    ON "Notifications" ("CreatedAt");
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "ProjectLabels" (
                    "Id" uuid NOT NULL,
                    "ProjectId" uuid NOT NULL,
                    "Name" character varying(100) NOT NULL,
                    "Color" character varying(7) NOT NULL,
                    "Order" integer NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT "PK_ProjectLabels" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ProjectLabels_Projects_ProjectId"
                        FOREIGN KEY ("ProjectId")
                        REFERENCES "Projects" ("Id")
                        ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS "IX_ProjectLabels_ProjectId"
                    ON "ProjectLabels" ("ProjectId");
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "TaskComments" (
                    "Id" uuid NOT NULL,
                    "TaskId" uuid NOT NULL,
                    "AuthorId" uuid NOT NULL,
                    "Text" character varying(5000) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT "PK_TaskComments" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_TaskComments_Tasks_TaskId"
                        FOREIGN KEY ("TaskId")
                        REFERENCES "Tasks" ("Id")
                        ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS "IX_TaskComments_TaskId"
                    ON "TaskComments" ("TaskId");
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "SubTasks" (
                    "Id" uuid NOT NULL,
                    "TaskId" uuid NOT NULL,
                    "Title" character varying(500) NOT NULL,
                    "IsCompleted" boolean NOT NULL,
                    "Order" integer NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT "PK_SubTasks" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_SubTasks_Tasks_TaskId"
                        FOREIGN KEY ("TaskId")
                        REFERENCES "Tasks" ("Id")
                        ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS "IX_SubTasks_TaskId"
                    ON "SubTasks" ("TaskId");
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "TaskLabelAssignments" (
                    "Id" uuid NOT NULL,
                    "TaskId" uuid NOT NULL,
                    "LabelId" uuid NOT NULL,
                    "AssignedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT "PK_TaskLabelAssignments" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_TaskLabelAssignments_ProjectLabels_LabelId"
                        FOREIGN KEY ("LabelId")
                        REFERENCES "ProjectLabels" ("Id")
                        ON DELETE CASCADE,
                    CONSTRAINT "FK_TaskLabelAssignments_Tasks_TaskId"
                        FOREIGN KEY ("TaskId")
                        REFERENCES "Tasks" ("Id")
                        ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS "IX_TaskLabelAssignments_LabelId"
                    ON "TaskLabelAssignments" ("LabelId");

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TaskLabelAssignments_TaskId_LabelId"
                    ON "TaskLabelAssignments" ("TaskId", "LabelId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ремонтная миграция intentionally no-op в Down, чтобы downgrade не удалял
            // таблицы и колонки, которые могли существовать легитимно в других окружениях.
        }
    }
}

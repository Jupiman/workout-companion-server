using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutCompanion.Server.Data.Migrations;

/// <inheritdoc />
public partial class StoreTimestampsAsUnixMilliseconds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE "__WorkoutSessions_UnixMs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkoutSessions" PRIMARY KEY AUTOINCREMENT,
                "SyncId" TEXT NOT NULL,
                "ProgramNameSnapshot" TEXT NOT NULL,
                "WorkoutNameSnapshot" TEXT NOT NULL,
                "StartedAt" INTEGER NOT NULL,
                "CompletedAt" INTEGER NOT NULL,
                "Status" TEXT NOT NULL,
                "PayloadSchemaVersion" INTEGER NOT NULL,
                "SourceDeviceId" TEXT NULL,
                "ReceivedAt" INTEGER NOT NULL,
                "LastReceivedAt" INTEGER NOT NULL
            );

            CREATE TABLE "__SessionExercises_UnixMs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SessionExercises" PRIMARY KEY AUTOINCREMENT,
                "SyncId" TEXT NOT NULL,
                "WorkoutSessionId" INTEGER NOT NULL,
                "SourceProgressionTrackSyncId" TEXT NULL,
                "ExerciseNameSnapshot" TEXT NOT NULL,
                "SortOrderSnapshot" INTEGER NOT NULL,
                "TrackingMode" TEXT NOT NULL,
                "RepMinSnapshot" INTEGER NULL,
                "RepMaxSnapshot" INTEGER NULL,
                "TargetRepsSnapshot" INTEGER NULL,
                "PrescribedWeightCentiKgSnapshot" INTEGER NULL,
                "TargetDurationSecondsSnapshot" INTEGER NULL,
                "ResultingProgressionWeightCentiKg" INTEGER NULL,
                "ResultingProgressionTargetReps" INTEGER NULL,
                "ResultingProgressionDurationSeconds" INTEGER NULL,
                CONSTRAINT "FK_SessionExercises_WorkoutSessions_WorkoutSessionId"
                    FOREIGN KEY ("WorkoutSessionId") REFERENCES "__WorkoutSessions_UnixMs" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "__SessionSets_UnixMs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SessionSets" PRIMARY KEY AUTOINCREMENT,
                "SyncId" TEXT NOT NULL,
                "SessionExerciseId" INTEGER NOT NULL,
                "SetOrder" INTEGER NOT NULL,
                "SetType" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "IsPlanned" INTEGER NOT NULL,
                "CountsForProgression" INTEGER NOT NULL,
                "PrescribedWeightCentiKg" INTEGER NULL,
                "PrescribedReps" INTEGER NULL,
                "PrescribedDurationSeconds" INTEGER NULL,
                "ActualWeightCentiKg" INTEGER NULL,
                "ActualReps" INTEGER NULL,
                "ActualDurationSeconds" INTEGER NULL,
                "CompletedAt" INTEGER NULL,
                CONSTRAINT "FK_SessionSets_SessionExercises_SessionExerciseId"
                    FOREIGN KEY ("SessionExerciseId") REFERENCES "__SessionExercises_UnixMs" ("Id") ON DELETE CASCADE
            );

            INSERT INTO "__WorkoutSessions_UnixMs" (
                "Id", "SyncId", "ProgramNameSnapshot", "WorkoutNameSnapshot",
                "StartedAt", "CompletedAt", "Status", "PayloadSchemaVersion",
                "SourceDeviceId", "ReceivedAt", "LastReceivedAt")
            SELECT
                "Id", "SyncId", "ProgramNameSnapshot", "WorkoutNameSnapshot",
                CAST(ROUND((julianday("StartedAt") - 2440587.5) * 86400000.0) AS INTEGER),
                CAST(ROUND((julianday("CompletedAt") - 2440587.5) * 86400000.0) AS INTEGER),
                "Status", "PayloadSchemaVersion", "SourceDeviceId",
                CAST(ROUND((julianday("ReceivedAt") - 2440587.5) * 86400000.0) AS INTEGER),
                CAST(ROUND((julianday("LastReceivedAt") - 2440587.5) * 86400000.0) AS INTEGER)
            FROM "WorkoutSessions";

            INSERT INTO "__SessionExercises_UnixMs"
            SELECT * FROM "SessionExercises";

            INSERT INTO "__SessionSets_UnixMs" (
                "Id", "SyncId", "SessionExerciseId", "SetOrder", "SetType", "Status",
                "IsPlanned", "CountsForProgression", "PrescribedWeightCentiKg",
                "PrescribedReps", "PrescribedDurationSeconds", "ActualWeightCentiKg",
                "ActualReps", "ActualDurationSeconds", "CompletedAt")
            SELECT
                "Id", "SyncId", "SessionExerciseId", "SetOrder", "SetType", "Status",
                "IsPlanned", "CountsForProgression", "PrescribedWeightCentiKg",
                "PrescribedReps", "PrescribedDurationSeconds", "ActualWeightCentiKg",
                "ActualReps", "ActualDurationSeconds",
                CASE
                    WHEN "CompletedAt" IS NULL THEN NULL
                    ELSE CAST(ROUND((julianday("CompletedAt") - 2440587.5) * 86400000.0) AS INTEGER)
                END
            FROM "SessionSets";

            DROP TABLE "SessionSets";
            DROP TABLE "SessionExercises";
            DROP TABLE "WorkoutSessions";

            ALTER TABLE "__WorkoutSessions_UnixMs" RENAME TO "WorkoutSessions";
            ALTER TABLE "__SessionExercises_UnixMs" RENAME TO "SessionExercises";
            ALTER TABLE "__SessionSets_UnixMs" RENAME TO "SessionSets";

            CREATE INDEX "IX_WorkoutSessions_CompletedAt" ON "WorkoutSessions" ("CompletedAt");
            CREATE INDEX "IX_WorkoutSessions_StartedAt" ON "WorkoutSessions" ("StartedAt");
            CREATE UNIQUE INDEX "IX_WorkoutSessions_SyncId" ON "WorkoutSessions" ("SyncId");
            CREATE INDEX "IX_SessionExercises_SourceProgressionTrackSyncId"
                ON "SessionExercises" ("SourceProgressionTrackSyncId");
            CREATE UNIQUE INDEX "IX_SessionExercises_SyncId" ON "SessionExercises" ("SyncId");
            CREATE INDEX "IX_SessionExercises_WorkoutSessionId_SortOrderSnapshot"
                ON "SessionExercises" ("WorkoutSessionId", "SortOrderSnapshot");
            CREATE INDEX "IX_SessionSets_SessionExerciseId_SetOrder"
                ON "SessionSets" ("SessionExerciseId", "SetOrder");
            CREATE UNIQUE INDEX "IX_SessionSets_SyncId" ON "SessionSets" ("SyncId");
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE "__WorkoutSessions_Text" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkoutSessions" PRIMARY KEY AUTOINCREMENT,
                "SyncId" TEXT NOT NULL,
                "ProgramNameSnapshot" TEXT NOT NULL,
                "WorkoutNameSnapshot" TEXT NOT NULL,
                "StartedAt" TEXT NOT NULL,
                "CompletedAt" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "PayloadSchemaVersion" INTEGER NOT NULL,
                "SourceDeviceId" TEXT NULL,
                "ReceivedAt" TEXT NOT NULL,
                "LastReceivedAt" TEXT NOT NULL
            );

            CREATE TABLE "__SessionExercises_Text" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SessionExercises" PRIMARY KEY AUTOINCREMENT,
                "SyncId" TEXT NOT NULL,
                "WorkoutSessionId" INTEGER NOT NULL,
                "SourceProgressionTrackSyncId" TEXT NULL,
                "ExerciseNameSnapshot" TEXT NOT NULL,
                "SortOrderSnapshot" INTEGER NOT NULL,
                "TrackingMode" TEXT NOT NULL,
                "RepMinSnapshot" INTEGER NULL,
                "RepMaxSnapshot" INTEGER NULL,
                "TargetRepsSnapshot" INTEGER NULL,
                "PrescribedWeightCentiKgSnapshot" INTEGER NULL,
                "TargetDurationSecondsSnapshot" INTEGER NULL,
                "ResultingProgressionWeightCentiKg" INTEGER NULL,
                "ResultingProgressionTargetReps" INTEGER NULL,
                "ResultingProgressionDurationSeconds" INTEGER NULL,
                CONSTRAINT "FK_SessionExercises_WorkoutSessions_WorkoutSessionId"
                    FOREIGN KEY ("WorkoutSessionId") REFERENCES "__WorkoutSessions_Text" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "__SessionSets_Text" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SessionSets" PRIMARY KEY AUTOINCREMENT,
                "SyncId" TEXT NOT NULL,
                "SessionExerciseId" INTEGER NOT NULL,
                "SetOrder" INTEGER NOT NULL,
                "SetType" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "IsPlanned" INTEGER NOT NULL,
                "CountsForProgression" INTEGER NOT NULL,
                "PrescribedWeightCentiKg" INTEGER NULL,
                "PrescribedReps" INTEGER NULL,
                "PrescribedDurationSeconds" INTEGER NULL,
                "ActualWeightCentiKg" INTEGER NULL,
                "ActualReps" INTEGER NULL,
                "ActualDurationSeconds" INTEGER NULL,
                "CompletedAt" TEXT NULL,
                CONSTRAINT "FK_SessionSets_SessionExercises_SessionExerciseId"
                    FOREIGN KEY ("SessionExerciseId") REFERENCES "__SessionExercises_Text" ("Id") ON DELETE CASCADE
            );

            INSERT INTO "__WorkoutSessions_Text" (
                "Id", "SyncId", "ProgramNameSnapshot", "WorkoutNameSnapshot",
                "StartedAt", "CompletedAt", "Status", "PayloadSchemaVersion",
                "SourceDeviceId", "ReceivedAt", "LastReceivedAt")
            SELECT
                "Id", "SyncId", "ProgramNameSnapshot", "WorkoutNameSnapshot",
                strftime('%Y-%m-%d %H:%M:%f', "StartedAt" / 1000.0, 'unixepoch') || '+00:00',
                strftime('%Y-%m-%d %H:%M:%f', "CompletedAt" / 1000.0, 'unixepoch') || '+00:00',
                "Status", "PayloadSchemaVersion", "SourceDeviceId",
                strftime('%Y-%m-%d %H:%M:%f', "ReceivedAt" / 1000.0, 'unixepoch') || '+00:00',
                strftime('%Y-%m-%d %H:%M:%f', "LastReceivedAt" / 1000.0, 'unixepoch') || '+00:00'
            FROM "WorkoutSessions";

            INSERT INTO "__SessionExercises_Text"
            SELECT * FROM "SessionExercises";

            INSERT INTO "__SessionSets_Text" (
                "Id", "SyncId", "SessionExerciseId", "SetOrder", "SetType", "Status",
                "IsPlanned", "CountsForProgression", "PrescribedWeightCentiKg",
                "PrescribedReps", "PrescribedDurationSeconds", "ActualWeightCentiKg",
                "ActualReps", "ActualDurationSeconds", "CompletedAt")
            SELECT
                "Id", "SyncId", "SessionExerciseId", "SetOrder", "SetType", "Status",
                "IsPlanned", "CountsForProgression", "PrescribedWeightCentiKg",
                "PrescribedReps", "PrescribedDurationSeconds", "ActualWeightCentiKg",
                "ActualReps", "ActualDurationSeconds",
                CASE
                    WHEN "CompletedAt" IS NULL THEN NULL
                    ELSE strftime('%Y-%m-%d %H:%M:%f', "CompletedAt" / 1000.0, 'unixepoch') || '+00:00'
                END
            FROM "SessionSets";

            DROP TABLE "SessionSets";
            DROP TABLE "SessionExercises";
            DROP TABLE "WorkoutSessions";

            ALTER TABLE "__WorkoutSessions_Text" RENAME TO "WorkoutSessions";
            ALTER TABLE "__SessionExercises_Text" RENAME TO "SessionExercises";
            ALTER TABLE "__SessionSets_Text" RENAME TO "SessionSets";

            CREATE INDEX "IX_WorkoutSessions_CompletedAt" ON "WorkoutSessions" ("CompletedAt");
            CREATE UNIQUE INDEX "IX_WorkoutSessions_SyncId" ON "WorkoutSessions" ("SyncId");
            CREATE INDEX "IX_SessionExercises_SourceProgressionTrackSyncId"
                ON "SessionExercises" ("SourceProgressionTrackSyncId");
            CREATE UNIQUE INDEX "IX_SessionExercises_SyncId" ON "SessionExercises" ("SyncId");
            CREATE INDEX "IX_SessionExercises_WorkoutSessionId_SortOrderSnapshot"
                ON "SessionExercises" ("WorkoutSessionId", "SortOrderSnapshot");
            CREATE INDEX "IX_SessionSets_SessionExerciseId_SetOrder"
                ON "SessionSets" ("SessionExerciseId", "SetOrder");
            CREATE UNIQUE INDEX "IX_SessionSets_SyncId" ON "SessionSets" ("SyncId");
            """);
    }
}

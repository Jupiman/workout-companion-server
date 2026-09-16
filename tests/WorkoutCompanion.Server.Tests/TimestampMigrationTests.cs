using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Tests;

public sealed class TimestampMigrationTests
{
    [Fact]
    public async Task Existing_text_timestamps_are_migrated_without_losing_data_or_relationships()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"workout-companion-migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "upgrade.db");
        var options = new DbContextOptionsBuilder<WorkoutDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                ForeignKeys = true,
            }.ToString())
            .Options;

        try
        {
            await using var context = new WorkoutDbContext(options);
            await CreatePreviousSchemaAsync(context);

            var workoutSyncId = Guid.Parse("11111111-1111-4111-8111-111111111111");
            var exerciseSyncId = Guid.Parse("22222222-2222-4222-8222-222222222222");
            var trackSyncId = Guid.Parse("33333333-3333-4333-8333-333333333333");
            var completedSetSyncId = Guid.Parse("44444444-4444-4444-8444-444444444444");
            var nullSetSyncId = Guid.Parse("55555555-5555-4555-8555-555555555555");
            await InsertPreviousSchemaDataAsync(
                context,
                workoutSyncId,
                exerciseSyncId,
                trackSyncId,
                completedSetSyncId,
                nullSetSyncId);

            await context.Database.MigrateAsync();
            context.ChangeTracker.Clear();

            var workout = await context.WorkoutSessions
                .AsNoTracking()
                .Include(session => session.Exercises)
                .ThenInclude(exercise => exercise.Sets)
                .SingleAsync();

            Assert.Equal(42, workout.Id);
            Assert.Equal(workoutSyncId, workout.SyncId);
            Assert.Equal(new DateTimeOffset(2026, 9, 15, 8, 0, 0, 123, TimeSpan.Zero).ToUnixTimeMilliseconds(), workout.CompletedAt.ToUnixTimeMilliseconds());
            Assert.Equal(new DateTimeOffset(2026, 9, 15, 7, 0, 0, 123, TimeSpan.Zero).ToUnixTimeMilliseconds(), workout.StartedAt.ToUnixTimeMilliseconds());
            Assert.Equal(new DateTimeOffset(2026, 9, 15, 8, 10, 11, 789, TimeSpan.Zero).ToUnixTimeMilliseconds(), workout.ReceivedAt.ToUnixTimeMilliseconds());
            Assert.Equal(workout.ReceivedAt, workout.LastReceivedAt);

            var exercise = Assert.Single(workout.Exercises);
            Assert.Equal(43, exercise.Id);
            Assert.Equal(exerciseSyncId, exercise.SyncId);
            Assert.Equal(trackSyncId, exercise.SourceProgressionTrackSyncId);
            Assert.Equal(42, exercise.WorkoutSessionId);
            Assert.Equal(2, exercise.Sets.Count);

            var completedSet = Assert.Single(exercise.Sets, set => set.SyncId == completedSetSyncId);
            Assert.Equal(44, completedSet.Id);
            Assert.Equal(43, completedSet.SessionExerciseId);
            Assert.Equal(new DateTimeOffset(2026, 9, 15, 7, 30, 0, 321, TimeSpan.Zero).ToUnixTimeMilliseconds(), completedSet.CompletedAt?.ToUnixTimeMilliseconds());
            var nullSet = Assert.Single(exercise.Sets, set => set.SyncId == nullSetSyncId);
            Assert.Equal(45, nullSet.Id);
            Assert.Null(nullSet.CompletedAt);

            Assert.Equal(
                ["integer", "integer", "integer", "integer"],
                await ReadTypesAsync(context, "SELECT typeof(StartedAt), typeof(CompletedAt), typeof(ReceivedAt), typeof(LastReceivedAt) FROM WorkoutSessions;"));
            Assert.Equal(
                ["integer", "null"],
                await ReadTypesAsync(context, "SELECT typeof(CompletedAt) FROM SessionSets ORDER BY Id;"));
            Assert.True(await IndexExistsAsync(context, "IX_WorkoutSessions_CompletedAt"));
            Assert.True(await IndexExistsAsync(context, "IX_WorkoutSessions_StartedAt"));
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Test cleanup must not hide the actual test result.
            }
            catch (UnauthorizedAccessException)
            {
                // Test cleanup must not hide the actual test result.
            }
        }
    }

    private static async Task CreatePreviousSchemaAsync(WorkoutDbContext context)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );

            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('202609150001_InitialCreate', '10.0.12');

            CREATE TABLE "WorkoutSessions" (
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

            CREATE TABLE "SessionExercises" (
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
                    FOREIGN KEY ("WorkoutSessionId") REFERENCES "WorkoutSessions" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "SessionSets" (
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
                    FOREIGN KEY ("SessionExerciseId") REFERENCES "SessionExercises" ("Id") ON DELETE CASCADE
            );

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
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertPreviousSchemaDataAsync(
        WorkoutDbContext context,
        Guid workoutSyncId,
        Guid exerciseSyncId,
        Guid trackSyncId,
        Guid completedSetSyncId,
        Guid nullSetSyncId)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            INSERT INTO WorkoutSessions (
                Id, SyncId, ProgramNameSnapshot, WorkoutNameSnapshot, StartedAt, CompletedAt,
                Status, PayloadSchemaVersion, SourceDeviceId, ReceivedAt, LastReceivedAt)
            VALUES (
                42, $workoutSyncId, 'Program', 'Workout',
                '2026-09-15 09:00:00.123+02:00', '2026-09-15 10:00:00.123+02:00',
                'Completed', 1, 'old-device',
                '2026-09-15 08:10:11.789+00:00', '2026-09-15 10:10:11.789+02:00');

            INSERT INTO SessionExercises (
                Id, SyncId, WorkoutSessionId, SourceProgressionTrackSyncId,
                ExerciseNameSnapshot, SortOrderSnapshot, TrackingMode,
                RepMinSnapshot, RepMaxSnapshot, TargetRepsSnapshot,
                PrescribedWeightCentiKgSnapshot, TargetDurationSecondsSnapshot,
                ResultingProgressionWeightCentiKg, ResultingProgressionTargetReps,
                ResultingProgressionDurationSeconds)
            VALUES (
                43, $exerciseSyncId, 42, $trackSyncId,
                'Bench Press', 0, 'WeightReps', 8, 10, 8, 7000, NULL, 7250, 8, NULL);

            INSERT INTO SessionSets (
                Id, SyncId, SessionExerciseId, SetOrder, SetType, Status,
                IsPlanned, CountsForProgression, PrescribedWeightCentiKg,
                PrescribedReps, PrescribedDurationSeconds, ActualWeightCentiKg,
                ActualReps, ActualDurationSeconds, CompletedAt)
            VALUES
                (44, $completedSetSyncId, 43, 0, 'Working', 'Completed', 1, 1, 7000, 8, NULL, 7000, 8, NULL, '2026-09-15 09:30:00.321+02:00'),
                (45, $nullSetSyncId, 43, 1, 'Working', 'Skipped', 1, 0, 7000, 8, NULL, NULL, NULL, NULL, NULL);
            """;
        AddParameter(command, "$workoutSyncId", workoutSyncId.ToString("D"));
        AddParameter(command, "$exerciseSyncId", exerciseSyncId.ToString("D"));
        AddParameter(command, "$trackSyncId", trackSyncId.ToString("D"));
        AddParameter(command, "$completedSetSyncId", completedSetSyncId.ToString("D"));
        AddParameter(command, "$nullSetSyncId", nullSetSyncId.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task<IReadOnlyList<string>> ReadTypesAsync(WorkoutDbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var types = new List<string>();
        while (await reader.ReadAsync())
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                types.Add(reader.GetString(index));
            }
        }

        return types;
    }

    private static async Task<bool> IndexExistsAsync(WorkoutDbContext context, string indexName)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_index_list('WorkoutSessions') WHERE name = $indexName;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$indexName";
        parameter.Value = indexName;
        command.Parameters.Add(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
    }
}

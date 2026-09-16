using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;
using WorkoutCompanion.Server.Data.Entities;
using WorkoutCompanion.Server.Pages.Analytics;
using WorkoutCompanion.Server.Pages.Export;

namespace WorkoutCompanion.Server.Tests;

public sealed class AnalyticsExportTests
{
    [Fact]
    public async Task Analytics_builds_calendar_streak_program_and_training_day_summaries()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        database.Context.WorkoutSessions.AddRange(
            CreateWorkout("Push", "Alpha", Utc(2026, 1, 1, 12), 60, 2),
            CreateWorkout("Push", "Alpha", Utc(2026, 1, 1, 18), 30, 1),
            CreateWorkout("Pull", "Alpha", Utc(2026, 1, 2, 12), 30, 1),
            CreateWorkout("Legs", "Beta", Utc(2026, 1, 4, 12), 90, 3),
            CreateWorkout("Outside", "Legacy", Utc(2025, 12, 31, 12), 45, 1));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var page = new AnalyticsModel(database.Context, TimeProvider.System) { Year = 2026 };

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(2026, page.SelectedYear);
        Assert.Equal(4, page.TotalWorkouts);
        Assert.Equal(3, page.ActiveDays);
        Assert.Equal(2, page.LongestStreak);
        Assert.Equal(7, page.TotalSets);
        Assert.Equal(TimeSpan.FromMinutes(210), page.TotalDuration);

        var januaryFirst = Assert.Single(page.CalendarDays, day =>
            day.Date == new DateOnly(2026, 1, 1));
        Assert.Equal(2, januaryFirst.WorkoutCount);
        Assert.Equal(4, januaryFirst.Intensity);

        var alpha = Assert.Single(page.Programs, program => program.ProgramName == "Alpha");
        Assert.Equal(3, alpha.SessionCount);
        Assert.Equal(2, alpha.ActiveDays);
        Assert.Equal(TimeSpan.FromMinutes(40), alpha.AverageDuration);
        Assert.Equal(4, alpha.SetCount);

        var push = Assert.Single(page.TrainingDays, day =>
            day.ProgramName == "Alpha" && day.WorkoutName == "Push");
        Assert.Equal(2, push.SessionCount);
        Assert.Equal(TimeSpan.FromMinutes(45), push.AverageDuration);
        Assert.Equal(1.5, push.AverageSetCount);
    }

    [Fact]
    public async Task Csv_export_applies_filters_escapes_text_and_preserves_negative_orders()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var included = CreateWorkout(
            "Bench, Day",
            "=Danger, \"Program\"",
            Utc(2026, 9, 15, 12),
            60,
            2,
            "Bench Press",
            WorkoutStatus.Completed,
            [CreateSet(-1, WorkoutSetType.Warmup), CreateSet(0, WorkoutSetType.Working)]);
        database.Context.WorkoutSessions.AddRange(
            included,
            CreateWorkout("Squat", "Other", Utc(2026, 9, 16, 12), 45, 1));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var page = new CsvHistoryExportModel(database.Context)
        {
            Program = "=Danger, \"Program\"",
            Status = "Completed",
            Exercise = "Bench Press",
            Search = "Bench",
        };

        var action = await page.OnGetAsync(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(action);
        var csv = Encoding.UTF8.GetString(file.FileContents).TrimStart('\uFEFF');
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.StartsWith("workoutSyncId,startedAt,completedAt", csv, StringComparison.Ordinal);
        Assert.Contains("\"'=Danger, \"\"Program\"\"\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Bench, Day\"", csv, StringComparison.Ordinal);
        Assert.Contains(",-1,WARMUP,COMPLETED,", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Other", csv, StringComparison.Ordinal);
        Assert.Equal(3, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task Json_export_is_nested_filtered_and_preserves_negative_orders()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        database.Context.WorkoutSessions.AddRange(
            CreateWorkout(
                "Bench Day",
                "Alpha",
                Utc(2026, 9, 15, 12),
                60,
                2,
                "Bench Press",
                WorkoutStatus.Completed,
                [CreateSet(-1, WorkoutSetType.Warmup), CreateSet(0, WorkoutSetType.Working)]),
            CreateWorkout("Outside", "Beta", Utc(2026, 9, 16, 12), 30, 1));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var page = new JsonHistoryExportModel(database.Context)
        {
            Program = "Alpha",
            DateFrom = new DateOnly(2026, 9, 15),
            DateTo = new DateOnly(2026, 9, 15),
        };

        var action = await page.OnGetAsync(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(action);
        using var json = JsonDocument.Parse(file.FileContents);
        var workout = Assert.Single(json.RootElement.EnumerateArray().ToArray());
        Assert.Equal("Alpha", workout.GetProperty("programName").GetString());
        var sets = workout.GetProperty("exercises")[0].GetProperty("sets");
        Assert.Equal(2, sets.GetArrayLength());
        Assert.Equal(-1, sets[0].GetProperty("setOrder").GetInt32());
        Assert.Equal("WARMUP", sets[0].GetProperty("setType").GetString());
    }

    private static WorkoutSessionEntity CreateWorkout(
        string workoutName,
        string programName,
        DateTimeOffset completedAt,
        int durationMinutes,
        int setCount,
        string exerciseName = "Exercise",
        WorkoutStatus status = WorkoutStatus.Completed,
        IReadOnlyList<SessionSetEntity>? sets = null)
    {
        sets ??= Enumerable.Range(0, setCount)
            .Select(index => CreateSet(index, WorkoutSetType.Working))
            .ToList();
        return new WorkoutSessionEntity
        {
            SyncId = Guid.NewGuid(),
            ProgramNameSnapshot = programName,
            WorkoutNameSnapshot = workoutName,
            StartedAt = completedAt.AddMinutes(-durationMinutes),
            CompletedAt = completedAt,
            Status = status,
            PayloadSchemaVersion = 1,
            ReceivedAt = completedAt.AddMinutes(1),
            LastReceivedAt = completedAt.AddMinutes(1),
            Exercises =
            [
                new SessionExerciseEntity
                {
                    SyncId = Guid.NewGuid(),
                    SourceProgressionTrackSyncId = Guid.NewGuid(),
                    ExerciseNameSnapshot = exerciseName,
                    SortOrderSnapshot = 0,
                    TrackingMode = TrackingMode.WeightReps,
                    Sets = sets.ToList(),
                },
            ],
        };
    }

    private static SessionSetEntity CreateSet(int order, WorkoutSetType type) => new()
    {
        SyncId = Guid.NewGuid(),
        SetOrder = order,
        SetType = type,
        Status = WorkoutSetStatus.Completed,
        IsPlanned = true,
        CountsForProgression = type != WorkoutSetType.Warmup,
        PrescribedWeightCentiKg = 7000,
        PrescribedReps = 8,
        ActualWeightCentiKg = 7000,
        ActualReps = 8,
        CompletedAt = Utc(2026, 9, 15, 11),
    };

    private static DateTimeOffset Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    private sealed class TemporarySqliteDatabase : IAsyncDisposable
    {
        private readonly string directory;

        private TemporarySqliteDatabase(string directory, WorkoutDbContext context)
        {
            this.directory = directory;
            Context = context;
        }

        public WorkoutDbContext Context { get; }

        public static async Task<TemporarySqliteDatabase> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"workout-companion-analytics-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(directory, "test.db"),
                ForeignKeys = true,
            }.ToString();
            var options = new DbContextOptionsBuilder<WorkoutDbContext>()
                .UseSqlite(connectionString)
                .Options;
            var context = new WorkoutDbContext(options);
            await context.Database.MigrateAsync();
            return new TemporarySqliteDatabase(directory, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // SQLite can release its final file handle shortly after the context is disposed.
            }
            catch (UnauthorizedAccessException)
            {
                // Test cleanup must not hide the actual test result.
            }
        }
    }
}

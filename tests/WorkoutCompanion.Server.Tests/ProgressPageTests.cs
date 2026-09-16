using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;
using WorkoutCompanion.Server.Data.Entities;
using WorkoutCompanion.Server.Pages.Progress;

namespace WorkoutCompanion.Server.Tests;

public sealed class ProgressPageTests
{
    [Fact]
    public async Task Progress_index_groups_tracks_and_uses_latest_progression_target()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var benchTrack = Guid.NewGuid();
        var squatTrack = Guid.NewGuid();
        database.Context.WorkoutSessions.AddRange(
            CreateWorkout("Bench Old", "Push", Utc(2026, 9, 1), benchTrack, "Bench Press", 7000, 8),
            CreateWorkout("Squat", "Legs", Utc(2026, 9, 2), squatTrack, "Back Squat", 10000, 5),
            CreateWorkout("Bench New", "Push", Utc(2026, 9, 3), benchTrack, "Bench Press", 7500, 8),
            CreateWorkout("Untracked", "Push", Utc(2026, 9, 4), null, "Push-up", null, 15));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var page = new ProgressIndexModel(database.Context);

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, page.Tracks.Count);
        var bench = page.Tracks[0];
        Assert.Equal(benchTrack, bench.TrackSyncId);
        Assert.Equal("Bench Press", bench.ExerciseName);
        Assert.Equal("Push", bench.ProgramName);
        Assert.Equal(7500, bench.CurrentWeightCentiKg);
        Assert.Equal(8, bench.CurrentReps);
        Assert.Equal(2, bench.SessionCount);
        Assert.Equal("75 kg × 8", ProgressIndexModel.FormatCurrentTarget(bench));
        Assert.Equal(squatTrack, page.Tracks[1].TrackSyncId);
    }

    [Fact]
    public async Task Progress_index_filters_by_exercise_and_activity_range()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var benchTrack = Guid.NewGuid();
        var squatTrack = Guid.NewGuid();
        database.Context.WorkoutSessions.AddRange(
            CreateWorkout("Bench", "Push", Utc(2026, 9, 1), benchTrack, "Bench Press", 7000, 8),
            CreateWorkout("Squat", "Legs", Utc(2026, 9, 2), squatTrack, "Back Squat", 10000, 5),
            CreateWorkout("Bench New", "Push", Utc(2026, 9, 3), benchTrack, "Bench Press", 7500, 8));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var page = new ProgressIndexModel(database.Context)
        {
            Exercise = " Squat ",
            DateFrom = new DateOnly(2026, 9, 2),
            DateTo = new DateOnly(2026, 9, 2),
        };

        await page.OnGetAsync(CancellationToken.None);

        var track = Assert.Single(page.Tracks);
        Assert.Equal(squatTrack, track.TrackSyncId);
        Assert.Equal("Squat", page.Exercise);
        Assert.Equal(1, track.SessionCount);
    }

    [Fact]
    public async Task Track_detail_computes_metrics_from_completed_non_warmup_sets()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var trackId = Guid.NewGuid();
        database.Context.WorkoutSessions.AddRange(
            CreateWorkout(
                "Bench Old",
                "Push",
                Utc(2026, 9, 1),
                trackId,
                "Bench Press",
                7250,
                8,
                CreateSet(-1, WorkoutSetType.Warmup, 5000, 5),
                CreateSet(0, WorkoutSetType.Working, 7000, 8),
                CreateSet(1, WorkoutSetType.Working, 7000, 7)),
            CreateWorkout(
                "Bench New",
                "Push",
                Utc(2026, 9, 3),
                trackId,
                "Bench Press",
                7750,
                6,
                CreateSet(0, WorkoutSetType.Working, 7500, 6),
                CreateSet(1, WorkoutSetType.Amrap, 7000, 10),
                CreateSet(2, WorkoutSetType.Working, 9000, 1, WorkoutSetStatus.Skipped)));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var page = new ProgressDetailsModel(database.Context);

        var action = await page.OnGetAsync(trackId, CancellationToken.None);

        Assert.IsType<PageResult>(action);
        Assert.NotNull(page.Track);
        Assert.Equal("Bench Press", page.Track.ExerciseName);
        Assert.Equal(2, page.Track.SessionCount);
        Assert.Equal("75 kg × 6", page.Track.HeaviestSet);
        Assert.Equal(10, page.Track.BestReps);
        Assert.Equal(93.33m, page.Track.BestEstimatedOneRepMax);
        Assert.Equal(["Bench New", "Bench Old"], page.Track.Entries.Select(entry => entry.WorkoutName));

        var newest = page.Track.Entries[0];
        Assert.Equal(75m, newest.WeightKg);
        Assert.Equal("6/10", newest.RepsSummary);
        Assert.Equal(1150m, newest.VolumeKg);
        Assert.Equal(93.33m, newest.EstimatedOneRepMaxKg);
        Assert.Equal(2, newest.SetCount);
        Assert.True(page.Track.WeightChart.HasData);
        Assert.True(page.Track.RepsChart.HasData);
        Assert.True(page.Track.EstimatedOneRepMaxChart.HasData);
        Assert.DoesNotContain("50", page.Track.HeaviestSet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Track_detail_date_filter_keeps_track_header_and_limits_sessions()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var trackId = Guid.NewGuid();
        database.Context.WorkoutSessions.AddRange(
            CreateWorkout("Old", "Push", Utc(2026, 9, 1), trackId, "Bench Press", 7000, 8),
            CreateWorkout("New", "Push", Utc(2026, 9, 3), trackId, "Bench Press", 7500, 8));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var page = new ProgressDetailsModel(database.Context)
        {
            DateFrom = new DateOnly(2026, 9, 3),
            DateTo = new DateOnly(2026, 9, 3),
        };

        var action = await page.OnGetAsync(trackId, CancellationToken.None);

        Assert.IsType<PageResult>(action);
        Assert.NotNull(page.Track);
        Assert.Equal("Bench Press", page.Track.ExerciseName);
        Assert.Equal("New", Assert.Single(page.Track.Entries).WorkoutName);
        Assert.Single(page.Track.WeightChart.Markers);
    }

    [Fact]
    public async Task Unknown_progress_track_returns_not_found()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var page = new ProgressDetailsModel(database.Context);

        var action = await page.OnGetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(action);
    }

    private static WorkoutSessionEntity CreateWorkout(
        string workoutName,
        string programName,
        DateTimeOffset completedAt,
        Guid? trackId,
        string exerciseName,
        int? resultingWeightCentiKg,
        int? resultingReps,
        params SessionSetEntity[] sets)
    {
        if (sets.Length == 0)
        {
            sets = [CreateSet(0, WorkoutSetType.Working, resultingWeightCentiKg, resultingReps)];
        }

        return new WorkoutSessionEntity
        {
            SyncId = Guid.NewGuid(),
            ProgramNameSnapshot = programName,
            WorkoutNameSnapshot = workoutName,
            StartedAt = completedAt.AddHours(-1),
            CompletedAt = completedAt,
            Status = WorkoutStatus.Completed,
            PayloadSchemaVersion = 1,
            ReceivedAt = completedAt.AddMinutes(1),
            LastReceivedAt = completedAt.AddMinutes(1),
            Exercises =
            [
                new SessionExerciseEntity
                {
                    SyncId = Guid.NewGuid(),
                    SourceProgressionTrackSyncId = trackId,
                    ExerciseNameSnapshot = exerciseName,
                    SortOrderSnapshot = 0,
                    TrackingMode = TrackingMode.WeightReps,
                    RepMinSnapshot = 6,
                    RepMaxSnapshot = 10,
                    TargetRepsSnapshot = resultingReps,
                    PrescribedWeightCentiKgSnapshot = resultingWeightCentiKg,
                    ResultingProgressionWeightCentiKg = resultingWeightCentiKg,
                    ResultingProgressionTargetReps = resultingReps,
                    Sets = sets.ToList(),
                },
            ],
        };
    }

    private static SessionSetEntity CreateSet(
        int setOrder,
        WorkoutSetType setType,
        int? weightCentiKg,
        int? reps,
        WorkoutSetStatus status = WorkoutSetStatus.Completed) => new()
    {
        SyncId = Guid.NewGuid(),
        SetOrder = setOrder,
        SetType = setType,
        Status = status,
        IsPlanned = true,
        CountsForProgression = setType != WorkoutSetType.Warmup,
        PrescribedWeightCentiKg = weightCentiKg,
        PrescribedReps = reps,
        ActualWeightCentiKg = weightCentiKg,
        ActualReps = reps,
        CompletedAt = status == WorkoutSetStatus.Completed ? Utc(2026, 9, 1) : null,
    };

    private static DateTimeOffset Utc(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

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
            var directory = Path.Combine(Path.GetTempPath(), $"workout-companion-progress-{Guid.NewGuid():N}");
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

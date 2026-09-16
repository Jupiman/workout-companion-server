using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;
using WorkoutCompanion.Server.Data.Entities;
using WorkoutCompanion.Server.Pages.History;
using WorkoutCompanion.Server.Pages.Workouts;

namespace WorkoutCompanion.Server.Tests;

public sealed class HistoryPageTests
{
    [Fact]
    public async Task History_is_uniquely_sorted_and_paginated_in_sqlite()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var firstCompletion = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var workouts = Enumerable.Range(0, 30)
            .Select(index => CreateWorkout($"Workout {index:00}", "Program", firstCompletion.AddDays(index)))
            .ToArray();
        database.Context.WorkoutSessions.AddRange(workouts);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var firstPage = new HistoryModel(database.Context);
        await firstPage.OnGetAsync(CancellationToken.None);
        var secondPage = new HistoryModel(database.Context) { PageNumber = 2 };
        await secondPage.OnGetAsync(CancellationToken.None);

        Assert.Equal(30, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(25, firstPage.Workouts.Count);
        Assert.Equal(5, secondPage.Workouts.Count);
        Assert.Equal("Workout 29", firstPage.Workouts[0].WorkoutName);
        Assert.Equal("Workout 05", firstPage.Workouts[^1].WorkoutName);
        Assert.Equal("Workout 04", secondPage.Workouts[0].WorkoutName);
        Assert.Empty(firstPage.Workouts.Select(workout => workout.SyncId)
            .Intersect(secondPage.Workouts.Select(workout => workout.SyncId)));
        Assert.All(firstPage.Workouts, workout =>
        {
            Assert.Equal(1, workout.ExerciseCount);
            Assert.Equal(1, workout.SetCount);
        });
    }

    [Fact]
    public async Task History_combines_date_program_status_workout_exercise_and_search_filters()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        database.Context.WorkoutSessions.AddRange(
            CreateWorkout("Push Day", "Alpha", Utc(2026, 9, 1), WorkoutStatus.Completed, "Bench Press"),
            CreateWorkout("Leg Day", "Alpha", Utc(2026, 9, 2), WorkoutStatus.Partial, "Hack Squat"),
            CreateWorkout("Leg Day", "Beta", Utc(2026, 9, 3), WorkoutStatus.Partial, "Hack Squat"),
            CreateWorkout("Pull Day", "Alpha", Utc(2026, 9, 4), WorkoutStatus.Partial, "Cable Row"));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var page = new HistoryModel(database.Context)
        {
            DateFrom = new DateOnly(2026, 9, 2),
            DateTo = new DateOnly(2026, 9, 3),
            Program = " Alpha ",
            WorkoutName = "Leg",
            Status = "Partial",
            Exercise = "Hack Squat",
            Search = "Squat",
        };

        await page.OnGetAsync(CancellationToken.None);

        var result = Assert.Single(page.Workouts);
        Assert.Equal("Leg Day", result.WorkoutName);
        Assert.Equal("Alpha", result.ProgramName);
        Assert.Equal(WorkoutStatus.Partial, result.Status);
        Assert.Contains("Alpha", page.ProgramOptions);
        Assert.Contains("Hack Squat", page.ExerciseOptions);
    }

    [Fact]
    public async Task Workout_detail_orders_exercises_and_preserves_negative_warmup_orders()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var workout = CreateWorkout("Strength Day", "Program", Utc(2026, 9, 15));
        workout.Exercises.Clear();
        workout.Exercises.Add(CreateExercise("Second Exercise", 1, [CreateSet(0, WorkoutSetType.Working)]));
        workout.Exercises.Add(CreateExercise(
            "First Exercise",
            0,
            [
                CreateSet(1, WorkoutSetType.Working),
                CreateSet(-1, WorkoutSetType.Warmup),
                CreateSet(0, WorkoutSetType.Working),
                CreateSet(-2, WorkoutSetType.Warmup),
            ]));
        database.Context.WorkoutSessions.Add(workout);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var page = new WorkoutDetailsModel(database.Context);

        var action = await page.OnGetAsync(workout.SyncId, CancellationToken.None);

        Assert.IsType<PageResult>(action);
        Assert.NotNull(page.Workout);
        Assert.Equal(["First Exercise", "Second Exercise"], page.Workout.Exercises.Select(exercise => exercise.Name));
        Assert.Equal([-2, -1, 0, 1], page.Workout.Exercises[0].Sets.Select(set => set.SetOrder));
        Assert.Equal(
            [WorkoutSetType.Warmup, WorkoutSetType.Warmup, WorkoutSetType.Working, WorkoutSetType.Working],
            page.Workout.Exercises[0].Sets.Select(set => set.SetType));
    }

    [Fact]
    public async Task Unknown_workout_detail_returns_not_found()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var page = new WorkoutDetailsModel(database.Context);

        var action = await page.OnGetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(action);
    }

    private static WorkoutSessionEntity CreateWorkout(
        string name,
        string program,
        DateTimeOffset completedAt,
        WorkoutStatus status = WorkoutStatus.Completed,
        string exerciseName = "Bench Press")
    {
        return new WorkoutSessionEntity
        {
            SyncId = Guid.NewGuid(),
            ProgramNameSnapshot = program,
            WorkoutNameSnapshot = name,
            StartedAt = completedAt.AddHours(-1),
            CompletedAt = completedAt,
            Status = status,
            PayloadSchemaVersion = 1,
            ReceivedAt = completedAt.AddMinutes(1),
            LastReceivedAt = completedAt.AddMinutes(1),
            Exercises = [CreateExercise(exerciseName, 0, [CreateSet(0, WorkoutSetType.Working)])],
        };
    }

    private static SessionExerciseEntity CreateExercise(
        string name,
        int sortOrder,
        IReadOnlyList<SessionSetEntity> sets) => new()
    {
        SyncId = Guid.NewGuid(),
        SourceProgressionTrackSyncId = Guid.NewGuid(),
        ExerciseNameSnapshot = name,
        SortOrderSnapshot = sortOrder,
        TrackingMode = TrackingMode.WeightReps,
        RepMinSnapshot = 8,
        RepMaxSnapshot = 10,
        TargetRepsSnapshot = 8,
        PrescribedWeightCentiKgSnapshot = 7000,
        ResultingProgressionWeightCentiKg = 7250,
        ResultingProgressionTargetReps = 8,
        Sets = sets.ToList(),
    };

    private static SessionSetEntity CreateSet(int order, WorkoutSetType setType) => new()
    {
        SyncId = Guid.NewGuid(),
        SetOrder = order,
        SetType = setType,
        Status = WorkoutSetStatus.Completed,
        IsPlanned = true,
        CountsForProgression = setType != WorkoutSetType.Warmup,
        PrescribedWeightCentiKg = 7000,
        PrescribedReps = 8,
        ActualWeightCentiKg = 7000,
        ActualReps = 8,
        CompletedAt = Utc(2026, 9, 15).AddMinutes(30),
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
            var directory = Path.Combine(Path.GetTempPath(), $"workout-companion-history-{Guid.NewGuid():N}");
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

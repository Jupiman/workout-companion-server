using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;
using WorkoutCompanion.Server.Data.Entities;
using WorkoutCompanion.Server.Pages;

namespace WorkoutCompanion.Server.Tests;

public sealed class SqliteTimestampTests
{
    [Fact]
    public async Task CompletedAt_ordering_and_range_filters_execute_in_sql_by_absolute_time()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        database.Context.WorkoutSessions.AddRange(
            CreateWorkout("A", new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(2))),
            CreateWorkout("B", new DateTimeOffset(2026, 9, 15, 9, 30, 0, TimeSpan.Zero)),
            CreateWorkout("C", new DateTimeOffset(2026, 9, 15, 8, 30, 0, TimeSpan.Zero)));
        await database.Context.SaveChangesAsync();

        var orderedQuery = database.Context.WorkoutSessions
            .AsNoTracking()
            .OrderByDescending(workout => workout.CompletedAt)
            .Select(workout => workout.WorkoutNameSnapshot);

        Assert.Contains("ORDER BY", orderedQuery.ToQueryString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["B", "C", "A"], await orderedQuery.ToListAsync());

        var startedAtQuery = database.Context.WorkoutSessions
            .AsNoTracking()
            .OrderBy(workout => workout.StartedAt)
            .Select(workout => workout.WorkoutNameSnapshot);
        Assert.Contains("ORDER BY", startedAtQuery.ToQueryString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["A", "C", "B"], await startedAtQuery.ToListAsync());

        var from = new DateTimeOffset(2026, 9, 15, 10, 15, 0, TimeSpan.FromHours(2));
        var to = new DateTimeOffset(2026, 9, 15, 11, 0, 0, TimeSpan.FromHours(2));
        var rangeQuery = database.Context.WorkoutSessions
            .AsNoTracking()
            .Where(workout => workout.CompletedAt >= from && workout.CompletedAt < to)
            .Select(workout => workout.WorkoutNameSnapshot);

        var rangeSql = rangeQuery.ToQueryString();
        Assert.Contains(">=", rangeSql, StringComparison.Ordinal);
        Assert.Contains("<", rangeSql, StringComparison.Ordinal);
        Assert.Equal(["C"], await rangeQuery.ToListAsync());
    }

    [Fact]
    public async Task Equivalent_offsets_store_the_same_unix_millisecond_value()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var localOffset = CreateWorkout(
            "Offset",
            new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(2)));
        var utc = CreateWorkout(
            "UTC",
            new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero));
        database.Context.WorkoutSessions.AddRange(localOffset, utc);
        await database.Context.SaveChangesAsync();

        var values = await ReadInt64ColumnAsync(
            database.Context,
            "SELECT CompletedAt FROM WorkoutSessions ORDER BY WorkoutNameSnapshot;");

        Assert.Equal(2, values.Count);
        Assert.Equal(values[0], values[1]);
        Assert.Equal(1_789_459_200_000L, values[0]);
    }

    [Fact]
    public async Task Dashboard_recent_workouts_query_executes_against_sqlite()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        database.Context.WorkoutSessions.AddRange(
            CreateWorkout("Older", new DateTimeOffset(2026, 9, 14, 20, 0, 0, TimeSpan.Zero)),
            CreateWorkout("Newest", new DateTimeOffset(2026, 9, 15, 20, 0, 0, TimeSpan.Zero)));
        await database.Context.SaveChangesAsync();
        var page = new IndexModel(database.Context);

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(["Newest", "Older"], page.RecentWorkouts.Select(workout => workout.WorkoutName));
    }

    [Fact]
    public async Task Nullable_set_timestamp_round_trips_as_unix_milliseconds()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var completedAt = new DateTimeOffset(2026, 9, 15, 10, 45, 12, 345, TimeSpan.FromHours(2));
        var workout = CreateWorkout("Set timestamps", completedAt.AddMinutes(15));
        workout.Exercises.Add(new SessionExerciseEntity
        {
            SyncId = Guid.NewGuid(),
            ExerciseNameSnapshot = "Bench Press",
            TrackingMode = TrackingMode.WeightReps,
            Sets =
            [
                CreateSet(0, completedAt),
                CreateSet(1, null),
            ],
        });
        database.Context.WorkoutSessions.Add(workout);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var storedSets = await database.Context.SessionSets
            .AsNoTracking()
            .OrderBy(set => set.SetOrder)
            .ToListAsync();
        var rawTypes = await ReadStringColumnAsync(
            database.Context,
            "SELECT typeof(CompletedAt) FROM SessionSets ORDER BY SetOrder;");

        Assert.Equal(completedAt.ToUnixTimeMilliseconds(), storedSets[0].CompletedAt?.ToUnixTimeMilliseconds());
        Assert.Equal(TimeSpan.Zero, storedSets[0].CompletedAt?.Offset);
        Assert.Null(storedSets[1].CompletedAt);
        Assert.Equal(["integer", "null"], rawTypes);
    }

    private static WorkoutSessionEntity CreateWorkout(string name, DateTimeOffset completedAt)
    {
        return new WorkoutSessionEntity
        {
            SyncId = Guid.NewGuid(),
            ProgramNameSnapshot = "Program",
            WorkoutNameSnapshot = name,
            StartedAt = completedAt.AddHours(-1),
            CompletedAt = completedAt,
            Status = WorkoutStatus.Completed,
            PayloadSchemaVersion = 1,
            ReceivedAt = completedAt.AddMinutes(1),
            LastReceivedAt = completedAt.AddMinutes(1),
        };
    }

    private static SessionSetEntity CreateSet(int order, DateTimeOffset? completedAt) => new()
    {
        SyncId = Guid.NewGuid(),
        SetOrder = order,
        SetType = WorkoutSetType.Working,
        Status = completedAt.HasValue ? WorkoutSetStatus.Completed : WorkoutSetStatus.Skipped,
        IsPlanned = true,
        CountsForProgression = completedAt.HasValue,
        CompletedAt = completedAt,
    };

    private static async Task<IReadOnlyList<long>> ReadInt64ColumnAsync(WorkoutDbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<long>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetInt64(0));
        }

        return values;
    }

    private static async Task<IReadOnlyList<string>> ReadStringColumnAsync(WorkoutDbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

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
            var directory = Path.Combine(Path.GetTempPath(), $"workout-companion-timestamps-{Guid.NewGuid():N}");
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
                // Test cleanup must not hide the actual test result.
            }
            catch (UnauthorizedAccessException)
            {
                // Test cleanup must not hide the actual test result.
            }
        }
    }
}

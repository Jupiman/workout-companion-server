using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Tests;

public sealed class WorkoutApiTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Repeated_upload_updates_one_authoritative_snapshot()
    {
        var workoutId = Guid.NewGuid();
        var first = CreateWorkout(workoutId, "Bench Press", 7000);
        var changed = CreateWorkout(workoutId, "Barbell Bench Press", 7250);
        using var client = CreateAuthenticatedClient();

        using var firstResponse = await PutWorkout(client, first);
        using var changedResponse = await PutWorkout(client, changed);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changedResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        var stored = await database.WorkoutSessions
            .AsNoTracking()
            .Include(session => session.Exercises)
            .ThenInclude(exercise => exercise.Sets)
            .SingleAsync(session => session.SyncId == workoutId);

        Assert.Single(stored.Exercises);
        Assert.Equal("Barbell Bench Press", stored.Exercises[0].ExerciseNameSnapshot);
        Assert.Equal(7250, stored.Exercises[0].Sets.Single().ActualWeightCentiKg);
    }

    [Fact]
    public async Task Active_workout_is_rejected()
    {
        var request = CreateWorkout(Guid.NewGuid(), "Plank", null) with
        {
            Status = WorkoutStatus.Active,
            CompletedAt = null,
        };
        using var client = CreateAuthenticatedClient();

        using var response = await PutWorkout(client, request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("COMPLETED or PARTIAL", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_batch_item_does_not_rollback_valid_item()
    {
        var validId = Guid.NewGuid();
        var valid = CreateWorkout(validId, "Machine Row", 6400);
        var invalid = CreateWorkout(Guid.NewGuid(), "Cable Row", 5000) with { CompletedAt = null };
        using var client = CreateAuthenticatedClient();
        var batch = new BatchWorkoutUploadRequest([valid, invalid]);

        using var response = await client.PostAsJsonAsync("/api/v1/workouts/batch", batch, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BatchWorkoutUploadResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Contains(result.Results, item => item.SyncId == validId && item.Status == "CREATED");
        Assert.Contains(result.Results, item => item.SyncId == invalid.SyncId && item.Status == "REJECTED");

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        Assert.True(await database.WorkoutSessions.AnyAsync(session => session.SyncId == validId));
        Assert.False(await database.WorkoutSessions.AnyAsync(session => session.SyncId == invalid.SyncId));
    }

    [Fact]
    public async Task Batch_with_mixed_warmup_and_working_sets_preserves_original_orders()
    {
        int[] expectedOrders = [-3, -2, -1, 0, 1, 2];
        var workoutId = Guid.NewGuid();
        var request = CreateWorkout(workoutId, "Bench Press", 7000);
        var exercise = request.Exercises!.Single();
        var templateSet = exercise.Sets!.Single();
        var sets = expectedOrders
            .Select(order => templateSet with
            {
                SyncId = Guid.NewGuid(),
                SetOrder = order,
                SetType = order < 0 ? WorkoutSetType.Warmup : WorkoutSetType.Working,
                CountsForProgression = order >= 0,
            })
            .ToArray();
        request = request with { Exercises = [exercise with { Sets = sets }] };
        using var client = CreateAuthenticatedClient();
        var batch = new BatchWorkoutUploadRequest([request]);

        using var response = await client.PostAsJsonAsync("/api/v1/workouts/batch", batch, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BatchWorkoutUploadResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Contains(result.Results, item => item.SyncId == workoutId && item.Status == "CREATED");

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        var storedSets = await database.SessionSets
            .AsNoTracking()
            .Where(set => set.SessionExercise.WorkoutSession.SyncId == workoutId)
            .OrderBy(set => set.SetOrder)
            .Select(set => new { set.SetOrder, set.SetType })
            .ToListAsync();

        Assert.Equal(expectedOrders, storedSets.Select(set => set.SetOrder));
        Assert.All(storedSets.Take(3), set => Assert.Equal(WorkoutSetType.Warmup, set.SetType));
        Assert.All(storedSets.Skip(3), set => Assert.Equal(WorkoutSetType.Working, set.SetType));
    }

    [Fact]
    public async Task Route_and_payload_sync_ids_must_match()
    {
        var request = CreateWorkout(Guid.NewGuid(), "Squat", 10000);
        using var client = CreateAuthenticatedClient();
        using var content = JsonContent.Create(request, options: JsonOptions);

        using var response = await client.PutAsync($"/api/v1/workouts/{Guid.NewGuid():D}", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_with_non_utc_offsets_preserves_absolute_instants()
    {
        var workoutId = Guid.NewGuid();
        var startedAt = new DateTimeOffset(2026, 9, 15, 10, 0, 0, 123, TimeSpan.FromHours(2));
        var completedAt = new DateTimeOffset(2026, 9, 15, 11, 15, 0, 456, TimeSpan.FromHours(2));
        var setCompletedAt = new DateTimeOffset(2026, 9, 15, 10, 30, 0, 789, TimeSpan.FromHours(2));
        var request = CreateWorkout(workoutId, "Offset Bench Press", 7000);
        var exercise = request.Exercises!.Single();
        var set = exercise.Sets!.Single() with { CompletedAt = setCompletedAt };
        request = request with
        {
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Exercises = [exercise with { Sets = [set] }],
        };
        using var client = CreateAuthenticatedClient();

        using var response = await PutWorkout(client, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        var stored = await database.WorkoutSessions
            .AsNoTracking()
            .Include(session => session.Exercises)
            .ThenInclude(storedExercise => storedExercise.Sets)
            .SingleAsync(session => session.SyncId == workoutId);

        Assert.Equal(startedAt.ToUnixTimeMilliseconds(), stored.StartedAt.ToUnixTimeMilliseconds());
        Assert.Equal(completedAt.ToUnixTimeMilliseconds(), stored.CompletedAt.ToUnixTimeMilliseconds());
        Assert.Equal(setCompletedAt.ToUnixTimeMilliseconds(), stored.Exercises.Single().Sets.Single().CompletedAt?.ToUnixTimeMilliseconds());
        Assert.Equal(TimeSpan.Zero, stored.StartedAt.Offset);
        Assert.Equal(TimeSpan.Zero, stored.CompletedAt.Offset);
        Assert.Equal(TimeSpan.Zero, stored.ReceivedAt.Offset);
        Assert.Equal(TimeSpan.Zero, stored.LastReceivedAt.Offset);

        await database.Database.OpenConnectionAsync();
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT typeof(StartedAt), typeof(CompletedAt), typeof(ReceivedAt), typeof(LastReceivedAt) FROM WorkoutSessions WHERE SyncId = $syncId;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$syncId";
        parameter.Value = workoutId.ToString("D");
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.All(Enumerable.Range(0, 4), index => Assert.Equal("integer", reader.GetString(index)));
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApplicationFactory.ApiToken);
        return client;
    }

    private static Task<HttpResponseMessage> PutWorkout(HttpClient client, WorkoutUploadRequest workout)
    {
        var content = JsonContent.Create(workout, options: JsonOptions);
        return client.PutAsync($"/api/v1/workouts/{workout.SyncId:D}", content);
    }

    private static WorkoutUploadRequest CreateWorkout(Guid workoutId, string exerciseName, int? weightCentiKg)
    {
        var started = new DateTimeOffset(2026, 9, 15, 7, 43, 0, TimeSpan.Zero);
        return new WorkoutUploadRequest(
            SchemaVersion: 1,
            SyncId: workoutId,
            ProgramName: "Current Program",
            WorkoutName: "Strength Day",
            StartedAt: started,
            CompletedAt: started.AddHours(1),
            Status: WorkoutStatus.Completed,
            Exercises:
            [
                new SessionExerciseUploadRequest(
                    SyncId: Guid.NewGuid(),
                    SourceProgressionTrackSyncId: Guid.NewGuid(),
                    ExerciseName: exerciseName,
                    SortOrder: 0,
                    TrackingMode: weightCentiKg.HasValue ? TrackingMode.WeightReps : TrackingMode.Duration,
                    RepMin: weightCentiKg.HasValue ? 8 : null,
                    RepMax: weightCentiKg.HasValue ? 10 : null,
                    TargetReps: weightCentiKg.HasValue ? 8 : null,
                    PrescribedWeightCentiKg: weightCentiKg,
                    TargetDurationSeconds: weightCentiKg.HasValue ? null : 60,
                    ResultingProgressionWeightCentiKg: weightCentiKg,
                    ResultingProgressionTargetReps: weightCentiKg.HasValue ? 9 : null,
                    ResultingProgressionDurationSeconds: weightCentiKg.HasValue ? null : 65,
                    Sets:
                    [
                        new SessionSetUploadRequest(
                            SyncId: Guid.NewGuid(),
                            SetOrder: 0,
                            SetType: WorkoutSetType.Working,
                            Status: WorkoutSetStatus.Completed,
                            IsPlanned: true,
                            CountsForProgression: true,
                            PrescribedWeightCentiKg: weightCentiKg,
                            PrescribedReps: weightCentiKg.HasValue ? 8 : null,
                            PrescribedDurationSeconds: weightCentiKg.HasValue ? null : 60,
                            ActualWeightCentiKg: weightCentiKg,
                            ActualReps: weightCentiKg.HasValue ? 8 : null,
                            ActualDurationSeconds: weightCentiKg.HasValue ? null : 60,
                            CompletedAt: started.AddMinutes(30))
                    ])
            ]);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

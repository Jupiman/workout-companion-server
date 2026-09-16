using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Ingestion;

namespace WorkoutCompanion.Server.Tests;

public sealed class WorkoutPayloadValidatorTests
{
    [Fact]
    public void Warmup_with_negative_order_is_accepted()
    {
        var request = CreateRequest(CreateSet(WorkoutSetType.Warmup, -1));

        var errors = new WorkoutPayloadValidator().Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Multiple_warmups_with_negative_orders_are_accepted()
    {
        var request = CreateRequest(
            CreateSet(WorkoutSetType.Warmup, -3),
            CreateSet(WorkoutSetType.Warmup, -2),
            CreateSet(WorkoutSetType.Warmup, -1));

        var errors = new WorkoutPayloadValidator().Validate(request);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(WorkoutSetType.Working)]
    [InlineData(WorkoutSetType.Extra)]
    [InlineData(WorkoutSetType.Amrap)]
    [InlineData(WorkoutSetType.Drop)]
    public void Non_warmup_with_negative_order_is_rejected(WorkoutSetType setType)
    {
        var set = CreateSet(setType, -1);
        var request = CreateRequest(set);

        var errors = new WorkoutPayloadValidator().Validate(request);

        Assert.Contains(
            $"Set '{set.SyncId:D}' has a negative setOrder but is not a WARMUP set.",
            errors);
    }

    [Fact]
    public void Working_sets_with_non_negative_orders_are_accepted()
    {
        var request = CreateRequest(
            CreateSet(WorkoutSetType.Working, 0),
            CreateSet(WorkoutSetType.Working, 1),
            CreateSet(WorkoutSetType.Working, 2));

        var errors = new WorkoutPayloadValidator().Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Duplicate_child_ids_are_rejected()
    {
        var duplicateSetId = Guid.NewGuid();
        var set = CreateSet(WorkoutSetType.Working, 0) with { SyncId = duplicateSetId };
        var request = CreateRequest(set, set);

        var errors = new WorkoutPayloadValidator().Validate(request);

        Assert.Contains(errors, error => error.Contains("Duplicate set syncId", StringComparison.Ordinal));
    }

    private static WorkoutUploadRequest CreateRequest(params SessionSetUploadRequest[] sets)
    {
        var started = new DateTimeOffset(2026, 9, 15, 7, 43, 0, TimeSpan.Zero);
        return new WorkoutUploadRequest(
            SchemaVersion: 1,
            SyncId: Guid.NewGuid(),
            ProgramName: "Program",
            WorkoutName: "Workout",
            StartedAt: started,
            CompletedAt: started.AddHours(1),
            Status: WorkoutStatus.Completed,
            Exercises:
            [
                new SessionExerciseUploadRequest(
                    SyncId: Guid.NewGuid(),
                    SourceProgressionTrackSyncId: Guid.NewGuid(),
                    ExerciseName: "Bench Press",
                    SortOrder: 0,
                    TrackingMode: TrackingMode.WeightReps,
                    RepMin: 8,
                    RepMax: 10,
                    TargetReps: 8,
                    PrescribedWeightCentiKg: 5000,
                    TargetDurationSeconds: null,
                    ResultingProgressionWeightCentiKg: 5250,
                    ResultingProgressionTargetReps: 8,
                    ResultingProgressionDurationSeconds: null,
                    Sets: sets)
            ]);
    }

    private static SessionSetUploadRequest CreateSet(WorkoutSetType setType, int setOrder)
    {
        var started = new DateTimeOffset(2026, 9, 15, 7, 43, 0, TimeSpan.Zero);
        return new SessionSetUploadRequest(
            SyncId: Guid.NewGuid(),
            SetOrder: setOrder,
            SetType: setType,
            Status: WorkoutSetStatus.Completed,
            IsPlanned: true,
            CountsForProgression: setType != WorkoutSetType.Warmup,
            PrescribedWeightCentiKg: 5000,
            PrescribedReps: 8,
            PrescribedDurationSeconds: null,
            ActualWeightCentiKg: 5000,
            ActualReps: 8,
            ActualDurationSeconds: null,
            CompletedAt: started.AddMinutes(20));
    }
}


using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Ingestion;

namespace WorkoutCompanion.Server.Tests;

public sealed class WorkoutPayloadValidatorTests
{
    [Fact]
    public void Duplicate_child_ids_are_rejected()
    {
        var duplicateSetId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var started = DateTimeOffset.UtcNow.AddHours(-1);
        var set = new SessionSetUploadRequest(
            duplicateSetId, 0, WorkoutSetType.Working, WorkoutSetStatus.Completed, true, true,
            5000, 8, null, 5000, 8, null, started.AddMinutes(20));
        var request = new WorkoutUploadRequest(
            1,
            Guid.NewGuid(),
            "Program",
            "Workout",
            started,
            started.AddHours(1),
            WorkoutStatus.Completed,
            [
                new SessionExerciseUploadRequest(
                    exerciseId, Guid.NewGuid(), "Bench Press", 0, TrackingMode.WeightReps,
                    8, 10, 8, 5000, null, 5250, 8, null, [set, set])
            ]);

        var errors = new WorkoutPayloadValidator().Validate(request);

        Assert.Contains(errors, error => error.Contains("Duplicate set syncId", StringComparison.Ordinal));
    }
}


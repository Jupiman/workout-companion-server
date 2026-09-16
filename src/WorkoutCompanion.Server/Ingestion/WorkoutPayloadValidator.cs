using WorkoutCompanion.Server.Contracts;

namespace WorkoutCompanion.Server.Ingestion;

public sealed class WorkoutPayloadValidator
{
    public const int MaximumBatchSize = 50;
    private const int MaximumExercises = 100;
    private const int MaximumSetsPerExercise = 100;

    public IReadOnlyList<string> Validate(WorkoutUploadRequest request, Guid? expectedSyncId = null)
    {
        var errors = new List<string>();

        if (request.SchemaVersion != 1)
        {
            errors.Add("schemaVersion must be 1.");
        }

        if (request.SyncId == Guid.Empty)
        {
            errors.Add("syncId must be a non-empty UUID.");
        }

        if (expectedSyncId.HasValue && expectedSyncId.Value != request.SyncId)
        {
            errors.Add("The route workoutSyncId must match the payload syncId.");
        }

        if (string.IsNullOrWhiteSpace(request.ProgramName))
        {
            errors.Add("programName is required.");
        }
        else if (request.ProgramName.Length > 200)
        {
            errors.Add("programName must not exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.WorkoutName))
        {
            errors.Add("workoutName is required.");
        }
        else if (request.WorkoutName.Length > 200)
        {
            errors.Add("workoutName must not exceed 200 characters.");
        }

        if (request.SourceDeviceId?.Length > 128)
        {
            errors.Add("sourceDeviceId must not exceed 128 characters.");
        }

        if (request.Status is not (WorkoutStatus.Completed or WorkoutStatus.Partial))
        {
            errors.Add("status must be COMPLETED or PARTIAL.");
        }

        if (request.CompletedAt is null)
        {
            errors.Add("completedAt is required for finalized workouts.");
        }
        else if (request.CompletedAt < request.StartedAt)
        {
            errors.Add("completedAt must not be earlier than startedAt.");
        }

        if (request.Exercises is null)
        {
            errors.Add("exercises is required.");
            return errors;
        }

        if (request.Exercises.Count > MaximumExercises)
        {
            errors.Add($"A workout may contain at most {MaximumExercises} exercises.");
        }

        var exerciseIds = new HashSet<Guid>();
        var setIds = new HashSet<Guid>();
        foreach (var exercise in request.Exercises)
        {
            ValidateExercise(exercise, errors, exerciseIds, setIds);
        }

        return errors;
    }

    private static void ValidateExercise(
        SessionExerciseUploadRequest exercise,
        List<string> errors,
        HashSet<Guid> exerciseIds,
        HashSet<Guid> setIds)
    {
        if (exercise.SyncId == Guid.Empty)
        {
            errors.Add("Every exercise syncId must be a non-empty UUID.");
        }
        else if (!exerciseIds.Add(exercise.SyncId))
        {
            errors.Add($"Duplicate exercise syncId '{exercise.SyncId:D}'.");
        }

        if (string.IsNullOrWhiteSpace(exercise.ExerciseName))
        {
            errors.Add($"Exercise '{exercise.SyncId:D}' must have an exerciseName.");
        }
        else if (exercise.ExerciseName.Length > 200)
        {
            errors.Add($"Exercise '{exercise.SyncId:D}' has an exerciseName longer than 200 characters.");
        }

        if (!Enum.IsDefined(exercise.TrackingMode))
        {
            errors.Add($"Exercise '{exercise.SyncId:D}' has an unsupported trackingMode.");
        }

        if (exercise.SortOrder < 0)
        {
            errors.Add($"Exercise '{exercise.SyncId:D}' has a negative sortOrder.");
        }

        ValidateNonNegative(exercise.RepMin, "repMin", exercise.SyncId, errors);
        ValidateNonNegative(exercise.RepMax, "repMax", exercise.SyncId, errors);
        ValidateNonNegative(exercise.TargetReps, "targetReps", exercise.SyncId, errors);
        ValidateNonNegative(exercise.PrescribedWeightCentiKg, "prescribedWeightCentiKg", exercise.SyncId, errors);
        ValidateNonNegative(exercise.TargetDurationSeconds, "targetDurationSeconds", exercise.SyncId, errors);
        ValidateNonNegative(exercise.ResultingProgressionWeightCentiKg, "resultingProgressionWeightCentiKg", exercise.SyncId, errors);
        ValidateNonNegative(exercise.ResultingProgressionTargetReps, "resultingProgressionTargetReps", exercise.SyncId, errors);
        ValidateNonNegative(exercise.ResultingProgressionDurationSeconds, "resultingProgressionDurationSeconds", exercise.SyncId, errors);

        if (exercise.RepMin.HasValue && exercise.RepMax.HasValue && exercise.RepMin > exercise.RepMax)
        {
            errors.Add($"Exercise '{exercise.SyncId:D}' has repMin greater than repMax.");
        }

        if (exercise.Sets is null)
        {
            errors.Add($"Exercise '{exercise.SyncId:D}' must include sets.");
            return;
        }

        if (exercise.Sets.Count > MaximumSetsPerExercise)
        {
            errors.Add($"Exercise '{exercise.SyncId:D}' may contain at most {MaximumSetsPerExercise} sets.");
        }

        foreach (var set in exercise.Sets)
        {
            if (set.SyncId == Guid.Empty)
            {
                errors.Add($"Every set in exercise '{exercise.SyncId:D}' must have a non-empty syncId.");
            }
            else if (!setIds.Add(set.SyncId))
            {
                errors.Add($"Duplicate set syncId '{set.SyncId:D}'.");
            }

            if (set.SetOrder < 0 && set.SetType != WorkoutSetType.Warmup)
            {
                errors.Add(
                    $"Set '{set.SyncId:D}' has a negative setOrder but is not a WARMUP set.");
            }

            if (!Enum.IsDefined(set.SetType))
            {
                errors.Add($"Set '{set.SyncId:D}' has an unsupported setType.");
            }

            if (!Enum.IsDefined(set.Status))
            {
                errors.Add($"Set '{set.SyncId:D}' has an unsupported status.");
            }

            ValidateNonNegative(set.PrescribedWeightCentiKg, "prescribedWeightCentiKg", set.SyncId, errors, "Set");
            ValidateNonNegative(set.PrescribedReps, "prescribedReps", set.SyncId, errors, "Set");
            ValidateNonNegative(set.PrescribedDurationSeconds, "prescribedDurationSeconds", set.SyncId, errors, "Set");
            ValidateNonNegative(set.ActualWeightCentiKg, "actualWeightCentiKg", set.SyncId, errors, "Set");
            ValidateNonNegative(set.ActualReps, "actualReps", set.SyncId, errors, "Set");
            ValidateNonNegative(set.ActualDurationSeconds, "actualDurationSeconds", set.SyncId, errors, "Set");
        }
    }

    private static void ValidateNonNegative(
        int? value,
        string field,
        Guid syncId,
        List<string> errors,
        string subject = "Exercise")
    {
        if (value < 0)
        {
            errors.Add($"{subject} '{syncId:D}' has a negative {field}.");
        }
    }
}

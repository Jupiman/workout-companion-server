using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;
using WorkoutCompanion.Server.Data.Entities;

namespace WorkoutCompanion.Server.Ingestion;

public enum IngestionOutcome
{
    Created,
    Updated,
}

public sealed class WorkoutIngestionService(WorkoutDbContext database, TimeProvider timeProvider)
{
    private static readonly SemaphoreSlim IngestionLock = new(1, 1);

    public async Task<IngestionOutcome> UpsertAsync(
        WorkoutUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        await IngestionLock.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var existing = await database.WorkoutSessions
                .AsSplitQuery()
                .Include(session => session.Exercises)
                .ThenInclude(exercise => exercise.Sets)
                .SingleOrDefaultAsync(session => session.SyncId == request.SyncId, cancellationToken);

            var now = timeProvider.GetUtcNow();
            if (existing is null)
            {
                var created = MapWorkout(request, now, now);
                database.WorkoutSessions.Add(created);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return IngestionOutcome.Created;
            }

            database.SessionExercises.RemoveRange(existing.Exercises);
            existing.Exercises.Clear();
            await database.SaveChangesAsync(cancellationToken);

            ApplyWorkout(existing, request, now);
            existing.Exercises.AddRange(request.Exercises!.Select(MapExercise));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return IngestionOutcome.Updated;
        }
        finally
        {
            IngestionLock.Release();
        }
    }

    private static WorkoutSessionEntity MapWorkout(
        WorkoutUploadRequest request,
        DateTimeOffset receivedAt,
        DateTimeOffset lastReceivedAt)
    {
        var entity = new WorkoutSessionEntity
        {
            SyncId = request.SyncId,
            ReceivedAt = receivedAt,
            LastReceivedAt = lastReceivedAt,
        };
        ApplyWorkout(entity, request, lastReceivedAt);
        entity.Exercises = request.Exercises!.Select(MapExercise).ToList();
        return entity;
    }

    private static void ApplyWorkout(
        WorkoutSessionEntity entity,
        WorkoutUploadRequest request,
        DateTimeOffset lastReceivedAt)
    {
        entity.ProgramNameSnapshot = request.ProgramName!.Trim();
        entity.WorkoutNameSnapshot = request.WorkoutName!.Trim();
        entity.StartedAt = request.StartedAt;
        entity.CompletedAt = request.CompletedAt!.Value;
        entity.Status = request.Status;
        entity.PayloadSchemaVersion = request.SchemaVersion;
        entity.SourceDeviceId = string.IsNullOrWhiteSpace(request.SourceDeviceId)
            ? null
            : request.SourceDeviceId.Trim();
        entity.LastReceivedAt = lastReceivedAt;
    }

    private static SessionExerciseEntity MapExercise(SessionExerciseUploadRequest request) => new()
    {
        SyncId = request.SyncId,
        SourceProgressionTrackSyncId = request.SourceProgressionTrackSyncId,
        ExerciseNameSnapshot = request.ExerciseName!.Trim(),
        SortOrderSnapshot = request.SortOrder,
        TrackingMode = request.TrackingMode,
        RepMinSnapshot = request.RepMin,
        RepMaxSnapshot = request.RepMax,
        TargetRepsSnapshot = request.TargetReps,
        PrescribedWeightCentiKgSnapshot = request.PrescribedWeightCentiKg,
        TargetDurationSecondsSnapshot = request.TargetDurationSeconds,
        ResultingProgressionWeightCentiKg = request.ResultingProgressionWeightCentiKg,
        ResultingProgressionTargetReps = request.ResultingProgressionTargetReps,
        ResultingProgressionDurationSeconds = request.ResultingProgressionDurationSeconds,
        Sets = request.Sets!.Select(MapSet).ToList(),
    };

    private static SessionSetEntity MapSet(SessionSetUploadRequest request) => new()
    {
        SyncId = request.SyncId,
        SetOrder = request.SetOrder,
        SetType = request.SetType,
        Status = request.Status,
        IsPlanned = request.IsPlanned,
        CountsForProgression = request.CountsForProgression,
        PrescribedWeightCentiKg = request.PrescribedWeightCentiKg,
        PrescribedReps = request.PrescribedReps,
        PrescribedDurationSeconds = request.PrescribedDurationSeconds,
        ActualWeightCentiKg = request.ActualWeightCentiKg,
        ActualReps = request.ActualReps,
        ActualDurationSeconds = request.ActualDurationSeconds,
        CompletedAt = request.CompletedAt,
    };
}

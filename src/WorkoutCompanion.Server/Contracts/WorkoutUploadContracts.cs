using System.Text.Json.Serialization;

namespace WorkoutCompanion.Server.Contracts;

public enum WorkoutStatus
{
    [JsonStringEnumMemberName("COMPLETED")]
    Completed,
    [JsonStringEnumMemberName("PARTIAL")]
    Partial,
    [JsonStringEnumMemberName("ACTIVE")]
    Active,
}

public enum TrackingMode
{
    [JsonStringEnumMemberName("WEIGHT_REPS")]
    WeightReps,
    [JsonStringEnumMemberName("REPS")]
    Reps,
    [JsonStringEnumMemberName("DURATION")]
    Duration,
}

public enum WorkoutSetType
{
    [JsonStringEnumMemberName("WARMUP")]
    Warmup,
    [JsonStringEnumMemberName("WORKING")]
    Working,
    [JsonStringEnumMemberName("EXTRA")]
    Extra,
    [JsonStringEnumMemberName("AMRAP")]
    Amrap,
    [JsonStringEnumMemberName("DROP")]
    Drop,
}

public enum WorkoutSetStatus
{
    [JsonStringEnumMemberName("PENDING")]
    Pending,
    [JsonStringEnumMemberName("COMPLETED")]
    Completed,
    [JsonStringEnumMemberName("SKIPPED")]
    Skipped,
}

public sealed record WorkoutUploadRequest(
    int SchemaVersion,
    Guid SyncId,
    string? ProgramName,
    string? WorkoutName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    WorkoutStatus Status,
    IReadOnlyList<SessionExerciseUploadRequest>? Exercises,
    string? SourceDeviceId = null);

public sealed record SessionExerciseUploadRequest(
    Guid SyncId,
    Guid? SourceProgressionTrackSyncId,
    string? ExerciseName,
    int SortOrder,
    TrackingMode TrackingMode,
    int? RepMin,
    int? RepMax,
    int? TargetReps,
    int? PrescribedWeightCentiKg,
    int? TargetDurationSeconds,
    int? ResultingProgressionWeightCentiKg,
    int? ResultingProgressionTargetReps,
    int? ResultingProgressionDurationSeconds,
    IReadOnlyList<SessionSetUploadRequest>? Sets);

public sealed record SessionSetUploadRequest(
    Guid SyncId,
    int SetOrder,
    WorkoutSetType SetType,
    WorkoutSetStatus Status,
    bool IsPlanned,
    bool CountsForProgression,
    int? PrescribedWeightCentiKg,
    int? PrescribedReps,
    int? PrescribedDurationSeconds,
    int? ActualWeightCentiKg,
    int? ActualReps,
    int? ActualDurationSeconds,
    DateTimeOffset? CompletedAt);

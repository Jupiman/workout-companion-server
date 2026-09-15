namespace WorkoutCompanion.Server.Contracts;

public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyList<string>? Details = null);

public sealed record ServerInfoResponse(
    string ServerVersion,
    int ApiVersion,
    int MinimumPayloadSchemaVersion,
    int MaximumPayloadSchemaVersion);

public sealed record WorkoutUploadResult(Guid SyncId, string Status);

public sealed record BatchWorkoutUploadRequest(IReadOnlyList<WorkoutUploadRequest?>? Workouts);

public sealed record BatchWorkoutUploadResult(
    Guid SyncId,
    string Status,
    string? Message = null,
    IReadOnlyList<string>? Details = null);

public sealed record BatchWorkoutUploadResponse(IReadOnlyList<BatchWorkoutUploadResult> Results);

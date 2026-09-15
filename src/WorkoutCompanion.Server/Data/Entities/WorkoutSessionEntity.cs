using WorkoutCompanion.Server.Contracts;

namespace WorkoutCompanion.Server.Data.Entities;

public sealed class WorkoutSessionEntity
{
    public long Id { get; set; }
    public Guid SyncId { get; set; }
    public string ProgramNameSnapshot { get; set; } = string.Empty;
    public string WorkoutNameSnapshot { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public WorkoutStatus Status { get; set; }
    public int PayloadSchemaVersion { get; set; }
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset LastReceivedAt { get; set; }
    public List<SessionExerciseEntity> Exercises { get; set; } = [];
}


using WorkoutCompanion.Server.Contracts;

namespace WorkoutCompanion.Server.Data.Entities;

public sealed class SessionSetEntity
{
    public long Id { get; set; }
    public Guid SyncId { get; set; }
    public long SessionExerciseId { get; set; }
    public SessionExerciseEntity SessionExercise { get; set; } = null!;
    public int SetOrder { get; set; }
    public WorkoutSetType SetType { get; set; }
    public WorkoutSetStatus Status { get; set; }
    public bool IsPlanned { get; set; }
    public bool CountsForProgression { get; set; }
    public int? PrescribedWeightCentiKg { get; set; }
    public int? PrescribedReps { get; set; }
    public int? PrescribedDurationSeconds { get; set; }
    public int? ActualWeightCentiKg { get; set; }
    public int? ActualReps { get; set; }
    public int? ActualDurationSeconds { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}


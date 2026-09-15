using WorkoutCompanion.Server.Contracts;

namespace WorkoutCompanion.Server.Data.Entities;

public sealed class SessionExerciseEntity
{
    public long Id { get; set; }
    public Guid SyncId { get; set; }
    public long WorkoutSessionId { get; set; }
    public WorkoutSessionEntity WorkoutSession { get; set; } = null!;
    public Guid? SourceProgressionTrackSyncId { get; set; }
    public string ExerciseNameSnapshot { get; set; } = string.Empty;
    public int SortOrderSnapshot { get; set; }
    public TrackingMode TrackingMode { get; set; }
    public int? RepMinSnapshot { get; set; }
    public int? RepMaxSnapshot { get; set; }
    public int? TargetRepsSnapshot { get; set; }
    public int? PrescribedWeightCentiKgSnapshot { get; set; }
    public int? TargetDurationSecondsSnapshot { get; set; }
    public int? ResultingProgressionWeightCentiKg { get; set; }
    public int? ResultingProgressionTargetReps { get; set; }
    public int? ResultingProgressionDurationSeconds { get; set; }
    public List<SessionSetEntity> Sets { get; set; } = [];
}


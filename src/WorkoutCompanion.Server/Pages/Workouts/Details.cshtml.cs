using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Pages.Workouts;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class WorkoutDetailsModel(WorkoutDbContext database) : PageModel
{
    public WorkoutDetail? Workout { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid syncId, CancellationToken cancellationToken)
    {
        Workout = await database.WorkoutSessions
            .AsNoTracking()
            .Where(workout => workout.SyncId == syncId)
            .Select(workout => new WorkoutDetail
            {
                SyncId = workout.SyncId,
                ProgramName = workout.ProgramNameSnapshot,
                WorkoutName = workout.WorkoutNameSnapshot,
                StartedAt = workout.StartedAt,
                CompletedAt = workout.CompletedAt,
                Status = workout.Status,
                Exercises = workout.Exercises
                    .OrderBy(exercise => exercise.SortOrderSnapshot)
                    .ThenBy(exercise => exercise.Id)
                    .Select(exercise => new ExerciseDetail
                    {
                        Name = exercise.ExerciseNameSnapshot,
                        TrackingMode = exercise.TrackingMode,
                        SourceProgressionTrackSyncId = exercise.SourceProgressionTrackSyncId,
                        PrescribedWeightCentiKg = exercise.PrescribedWeightCentiKgSnapshot,
                        TargetReps = exercise.TargetRepsSnapshot,
                        TargetDurationSeconds = exercise.TargetDurationSecondsSnapshot,
                        ResultingProgressionWeightCentiKg = exercise.ResultingProgressionWeightCentiKg,
                        ResultingProgressionTargetReps = exercise.ResultingProgressionTargetReps,
                        ResultingProgressionDurationSeconds = exercise.ResultingProgressionDurationSeconds,
                        Sets = exercise.Sets
                            .OrderBy(set => set.SetOrder)
                            .ThenBy(set => set.Id)
                            .Select(set => new SetDetail
                            {
                                SetOrder = set.SetOrder,
                                SetType = set.SetType,
                                Status = set.Status,
                                PrescribedWeightCentiKg = set.PrescribedWeightCentiKg,
                                PrescribedReps = set.PrescribedReps,
                                PrescribedDurationSeconds = set.PrescribedDurationSeconds,
                                ActualWeightCentiKg = set.ActualWeightCentiKg,
                                ActualReps = set.ActualReps,
                                ActualDurationSeconds = set.ActualDurationSeconds,
                            })
                            .ToList(),
                    })
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return Workout is null ? NotFound() : Page();
    }

    public static string FormatSetLabel(SetDetail set) => set.SetType == WorkoutSetType.Warmup
        ? $"W{Math.Abs((long)set.SetOrder)}"
        : (set.SetOrder + 1).ToString(CultureInfo.InvariantCulture);

    public static string FormatSetType(WorkoutSetType setType) => setType == WorkoutSetType.Warmup
        ? "Warm-up"
        : setType.ToString();

    public static string FormatPerformance(
        TrackingMode mode,
        int? weightCentiKg,
        int? reps,
        int? durationSeconds)
    {
        return mode switch
        {
            TrackingMode.WeightReps => weightCentiKg.HasValue || reps.HasValue
                ? $"{FormatWeight(weightCentiKg)} × {reps?.ToString(CultureInfo.InvariantCulture) ?? "—"}"
                : "—",
            TrackingMode.Reps => reps?.ToString(CultureInfo.InvariantCulture) ?? "—",
            TrackingMode.Duration => durationSeconds.HasValue
                ? TimeSpan.FromSeconds(durationSeconds.Value).ToString(@"m\:ss", CultureInfo.InvariantCulture)
                : "—",
            _ => "—",
        };
    }

    public static string FormatProgression(ExerciseDetail exercise)
    {
        var before = FormatPerformance(
            exercise.TrackingMode,
            exercise.PrescribedWeightCentiKg,
            exercise.TargetReps,
            exercise.TargetDurationSeconds);
        var after = FormatPerformance(
            exercise.TrackingMode,
            exercise.ResultingProgressionWeightCentiKg,
            exercise.ResultingProgressionTargetReps,
            exercise.ResultingProgressionDurationSeconds);
        return $"{before} → {after}";
    }

    private static string FormatWeight(int? weightCentiKg) => weightCentiKg.HasValue
        ? $"{(weightCentiKg.Value / 100m).ToString("0.##", CultureInfo.InvariantCulture)} kg"
        : "—";

    public sealed class WorkoutDetail
    {
        public Guid SyncId { get; init; }
        public required string ProgramName { get; init; }
        public required string WorkoutName { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
        public WorkoutStatus Status { get; init; }
        public required IReadOnlyList<ExerciseDetail> Exercises { get; init; }
        public TimeSpan Duration => CompletedAt - StartedAt;
    }

    public sealed class ExerciseDetail
    {
        public required string Name { get; init; }
        public TrackingMode TrackingMode { get; init; }
        public Guid? SourceProgressionTrackSyncId { get; init; }
        public int? PrescribedWeightCentiKg { get; init; }
        public int? TargetReps { get; init; }
        public int? TargetDurationSeconds { get; init; }
        public int? ResultingProgressionWeightCentiKg { get; init; }
        public int? ResultingProgressionTargetReps { get; init; }
        public int? ResultingProgressionDurationSeconds { get; init; }
        public required IReadOnlyList<SetDetail> Sets { get; init; }
        public bool HasProgressionChange =>
            PrescribedWeightCentiKg != ResultingProgressionWeightCentiKg
            || TargetReps != ResultingProgressionTargetReps
            || TargetDurationSeconds != ResultingProgressionDurationSeconds;
    }

    public sealed class SetDetail
    {
        public int SetOrder { get; init; }
        public WorkoutSetType SetType { get; init; }
        public WorkoutSetStatus Status { get; init; }
        public int? PrescribedWeightCentiKg { get; init; }
        public int? PrescribedReps { get; init; }
        public int? PrescribedDurationSeconds { get; init; }
        public int? ActualWeightCentiKg { get; init; }
        public int? ActualReps { get; init; }
        public int? ActualDurationSeconds { get; init; }
    }
}

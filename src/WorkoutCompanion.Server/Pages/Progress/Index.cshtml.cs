using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Pages.Progress;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class ProgressIndexModel(WorkoutDbContext database) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Exercise { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateTo { get; set; }

    public IReadOnlyList<ProgressTrackSummary> Tracks { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Exercise = string.IsNullOrWhiteSpace(Exercise) ? null : Exercise.Trim();
        var query = database.SessionExercises
            .AsNoTracking()
            .Where(exercise => exercise.SourceProgressionTrackSyncId.HasValue);

        if (Exercise is not null)
        {
            query = query.Where(exercise => exercise.ExerciseNameSnapshot.Contains(Exercise));
        }

        if (DateFrom.HasValue)
        {
            var from = ToUtcStart(DateFrom.Value);
            query = query.Where(exercise => exercise.WorkoutSession.CompletedAt >= from);
        }

        if (DateTo.HasValue)
        {
            var toExclusive = ToUtcStart(DateTo.Value.AddDays(1));
            query = query.Where(exercise => exercise.WorkoutSession.CompletedAt < toExclusive);
        }

        Tracks = await query
            .GroupBy(exercise => exercise.SourceProgressionTrackSyncId!.Value)
            .Select(group => new ProgressTrackSummary
            {
                TrackSyncId = group.Key,
                ExerciseName = group
                    .OrderByDescending(exercise => exercise.WorkoutSession.CompletedAt)
                    .ThenByDescending(exercise => exercise.Id)
                    .Select(exercise => exercise.ExerciseNameSnapshot)
                    .First(),
                ProgramName = group
                    .OrderByDescending(exercise => exercise.WorkoutSession.CompletedAt)
                    .ThenByDescending(exercise => exercise.Id)
                    .Select(exercise => exercise.WorkoutSession.ProgramNameSnapshot)
                    .First(),
                TrackingMode = group
                    .OrderByDescending(exercise => exercise.WorkoutSession.CompletedAt)
                    .ThenByDescending(exercise => exercise.Id)
                    .Select(exercise => exercise.TrackingMode)
                    .First(),
                CurrentWeightCentiKg = group
                    .OrderByDescending(exercise => exercise.WorkoutSession.CompletedAt)
                    .ThenByDescending(exercise => exercise.Id)
                    .Select(exercise => exercise.ResultingProgressionWeightCentiKg
                        ?? exercise.PrescribedWeightCentiKgSnapshot)
                    .First(),
                CurrentReps = group
                    .OrderByDescending(exercise => exercise.WorkoutSession.CompletedAt)
                    .ThenByDescending(exercise => exercise.Id)
                    .Select(exercise => exercise.ResultingProgressionTargetReps
                        ?? exercise.TargetRepsSnapshot)
                    .First(),
                CurrentDurationSeconds = group
                    .OrderByDescending(exercise => exercise.WorkoutSession.CompletedAt)
                    .ThenByDescending(exercise => exercise.Id)
                    .Select(exercise => exercise.ResultingProgressionDurationSeconds
                        ?? exercise.TargetDurationSecondsSnapshot)
                    .First(),
                LastPerformed = group.Max(exercise => exercise.WorkoutSession.CompletedAt),
                SessionCount = group
                    .Select(exercise => exercise.WorkoutSessionId)
                    .Distinct()
                    .Count(),
            })
            .OrderByDescending(track => track.LastPerformed)
            .ThenBy(track => track.TrackSyncId)
            .ToListAsync(cancellationToken);
    }

    public static string FormatCurrentTarget(ProgressTrackSummary track) => track.TrackingMode switch
    {
        TrackingMode.WeightReps => track.CurrentWeightCentiKg.HasValue || track.CurrentReps.HasValue
            ? $"{FormatWeight(track.CurrentWeightCentiKg)} × {track.CurrentReps?.ToString(CultureInfo.InvariantCulture) ?? "—"}"
            : "—",
        TrackingMode.Reps => track.CurrentReps?.ToString(CultureInfo.InvariantCulture) ?? "—",
        TrackingMode.Duration => track.CurrentDurationSeconds.HasValue
            ? TimeSpan.FromSeconds(track.CurrentDurationSeconds.Value).ToString(@"m\:ss", CultureInfo.InvariantCulture)
            : "—",
        _ => "—",
    };

    private static string FormatWeight(int? weightCentiKg) => weightCentiKg.HasValue
        ? $"{(weightCentiKg.Value / 100m).ToString("0.##", CultureInfo.InvariantCulture)} kg"
        : "—";

    private static DateTimeOffset ToUtcStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public sealed class ProgressTrackSummary
    {
        public Guid TrackSyncId { get; init; }
        public required string ExerciseName { get; init; }
        public required string ProgramName { get; init; }
        public TrackingMode TrackingMode { get; init; }
        public int? CurrentWeightCentiKg { get; init; }
        public int? CurrentReps { get; init; }
        public int? CurrentDurationSeconds { get; init; }
        public DateTimeOffset LastPerformed { get; init; }
        public int SessionCount { get; init; }
    }
}

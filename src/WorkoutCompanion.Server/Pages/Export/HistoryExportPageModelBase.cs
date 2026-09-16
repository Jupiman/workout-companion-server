using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Pages.Export;

public abstract class HistoryExportPageModelBase(WorkoutDbContext database) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Program { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? WorkoutName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Exercise { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    protected async Task<IReadOnlyList<ExportWorkout>> LoadWorkoutsAsync(
        CancellationToken cancellationToken)
    {
        Program = Normalize(Program);
        WorkoutName = Normalize(WorkoutName);
        Status = Normalize(Status);
        Exercise = Normalize(Exercise);
        Search = Normalize(Search);

        var query = database.WorkoutSessions
            .AsNoTracking()
            .Where(workout => workout.Status == WorkoutStatus.Completed
                || workout.Status == WorkoutStatus.Partial);

        if (DateFrom.HasValue)
        {
            var from = ToUtcStart(DateFrom.Value);
            query = query.Where(workout => workout.CompletedAt >= from);
        }

        if (DateTo.HasValue)
        {
            var toExclusive = ToUtcStart(DateTo.Value.AddDays(1));
            query = query.Where(workout => workout.CompletedAt < toExclusive);
        }

        if (Program is not null)
        {
            query = query.Where(workout => workout.ProgramNameSnapshot == Program);
        }

        if (WorkoutName is not null)
        {
            query = query.Where(workout => workout.WorkoutNameSnapshot.Contains(WorkoutName));
        }

        if (Enum.TryParse<WorkoutStatus>(Status, ignoreCase: true, out var status)
            && status is WorkoutStatus.Completed or WorkoutStatus.Partial)
        {
            query = query.Where(workout => workout.Status == status);
        }

        if (Exercise is not null)
        {
            query = query.Where(workout =>
                workout.Exercises.Any(item => item.ExerciseNameSnapshot == Exercise));
        }

        if (Search is not null)
        {
            query = query.Where(workout =>
                workout.WorkoutNameSnapshot.Contains(Search)
                || workout.ProgramNameSnapshot.Contains(Search)
                || workout.Exercises.Any(item => item.ExerciseNameSnapshot.Contains(Search)));
        }

        return await query
            .OrderByDescending(workout => workout.CompletedAt)
            .ThenByDescending(workout => workout.Id)
            .Select(workout => new ExportWorkout
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
                    .Select(exercise => new ExportExercise
                    {
                        SyncId = exercise.SyncId,
                        ProgressionTrackSyncId = exercise.SourceProgressionTrackSyncId,
                        Name = exercise.ExerciseNameSnapshot,
                        SortOrder = exercise.SortOrderSnapshot,
                        TrackingMode = exercise.TrackingMode,
                        Sets = exercise.Sets
                            .OrderBy(set => set.SetOrder)
                            .ThenBy(set => set.Id)
                            .Select(set => new ExportSet
                            {
                                SyncId = set.SyncId,
                                SetOrder = set.SetOrder,
                                SetType = set.SetType,
                                Status = set.Status,
                                PrescribedWeightCentiKg = set.PrescribedWeightCentiKg,
                                PrescribedReps = set.PrescribedReps,
                                PrescribedDurationSeconds = set.PrescribedDurationSeconds,
                                ActualWeightCentiKg = set.ActualWeightCentiKg,
                                ActualReps = set.ActualReps,
                                ActualDurationSeconds = set.ActualDurationSeconds,
                                CompletedAt = set.CompletedAt,
                            })
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset ToUtcStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}

public sealed class ExportWorkout
{
    public Guid SyncId { get; init; }
    public required string ProgramName { get; init; }
    public required string WorkoutName { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public WorkoutStatus Status { get; init; }
    public required IReadOnlyList<ExportExercise> Exercises { get; init; }
}

public sealed class ExportExercise
{
    public Guid SyncId { get; init; }
    public Guid? ProgressionTrackSyncId { get; init; }
    public required string Name { get; init; }
    public int SortOrder { get; init; }
    public TrackingMode TrackingMode { get; init; }
    public required IReadOnlyList<ExportSet> Sets { get; init; }
}

public sealed class ExportSet
{
    public Guid SyncId { get; init; }
    public int SetOrder { get; init; }
    public WorkoutSetType SetType { get; init; }
    public WorkoutSetStatus Status { get; init; }
    public int? PrescribedWeightCentiKg { get; init; }
    public int? PrescribedReps { get; init; }
    public int? PrescribedDurationSeconds { get; init; }
    public int? ActualWeightCentiKg { get; init; }
    public int? ActualReps { get; init; }
    public int? ActualDurationSeconds { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Pages.History;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class HistoryModel(WorkoutDbContext database) : PageModel
{
    public const int PageSize = 25;

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

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public IReadOnlyList<WorkoutSummary> Workouts { get; private set; } = [];
    public IReadOnlyList<string> ProgramOptions { get; private set; } = [];
    public IReadOnlyList<string> ExerciseOptions { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int ResultStart => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int ResultEnd => Math.Min(PageNumber * PageSize, TotalCount);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Program = Normalize(Program);
        WorkoutName = Normalize(WorkoutName);
        Status = Normalize(Status);
        Exercise = Normalize(Exercise);
        Search = Normalize(Search);

        ProgramOptions = await database.WorkoutSessions
            .AsNoTracking()
            .Select(workout => workout.ProgramNameSnapshot)
            .Distinct()
            .OrderBy(program => program)
            .ToListAsync(cancellationToken);
        ExerciseOptions = await database.SessionExercises
            .AsNoTracking()
            .Select(exercise => exercise.ExerciseNameSnapshot)
            .Distinct()
            .OrderBy(exercise => exercise)
            .ToListAsync(cancellationToken);

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
            query = query.Where(workout => workout.Exercises.Any(item => item.ExerciseNameSnapshot == Exercise));
        }

        if (Search is not null)
        {
            query = query.Where(workout =>
                workout.WorkoutNameSnapshot.Contains(Search)
                || workout.ProgramNameSnapshot.Contains(Search)
                || workout.Exercises.Any(item => item.ExerciseNameSnapshot.Contains(Search)));
        }

        TotalCount = await query.CountAsync(cancellationToken);
        TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
        PageNumber = Math.Clamp(PageNumber, 1, Math.Max(TotalPages, 1));

        Workouts = await query
            .OrderByDescending(workout => workout.CompletedAt)
            .ThenByDescending(workout => workout.Id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(workout => new WorkoutSummary
            {
                SyncId = workout.SyncId,
                StartedAt = workout.StartedAt,
                CompletedAt = workout.CompletedAt,
                ProgramName = workout.ProgramNameSnapshot,
                WorkoutName = workout.WorkoutNameSnapshot,
                Status = workout.Status,
                ExerciseCount = workout.Exercises.Count,
                SetCount = workout.Exercises.SelectMany(exercise => exercise.Sets).Count(),
            })
            .ToListAsync(cancellationToken);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset ToUtcStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public sealed class WorkoutSummary
    {
        public Guid SyncId { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
        public required string ProgramName { get; init; }
        public required string WorkoutName { get; init; }
        public WorkoutStatus Status { get; init; }
        public int ExerciseCount { get; init; }
        public int SetCount { get; init; }
        public TimeSpan Duration => CompletedAt - StartedAt;
    }
}

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Pages;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class IndexModel(WorkoutDbContext database) : PageModel
{
    public int WorkoutCount { get; private set; }
    public int ExerciseCount { get; private set; }
    public int SetCount { get; private set; }
    public IReadOnlyList<RecentWorkout> RecentWorkouts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        WorkoutCount = await database.WorkoutSessions.AsNoTracking().CountAsync(cancellationToken);
        ExerciseCount = await database.SessionExercises.AsNoTracking().CountAsync(cancellationToken);
        SetCount = await database.SessionSets.AsNoTracking().CountAsync(cancellationToken);
        RecentWorkouts = await database.WorkoutSessions
            .AsNoTracking()
            .OrderByDescending(workout => workout.CompletedAt)
            .ThenByDescending(workout => workout.Id)
            .Take(8)
            .Select(workout => new RecentWorkout(
                workout.SyncId,
                workout.WorkoutNameSnapshot,
                workout.ProgramNameSnapshot,
                workout.CompletedAt,
                workout.Status))
            .ToListAsync(cancellationToken);
    }

    public sealed record RecentWorkout(
        Guid SyncId,
        string WorkoutName,
        string ProgramName,
        DateTimeOffset CompletedAt,
        WorkoutStatus Status);
}


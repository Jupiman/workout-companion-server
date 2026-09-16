using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Pages.Analytics;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class AnalyticsModel(WorkoutDbContext database, TimeProvider timeProvider) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    public int SelectedYear { get; private set; }
    public int TotalWorkouts { get; private set; }
    public int ActiveDays { get; private set; }
    public int LongestStreak { get; private set; }
    public int TotalSets { get; private set; }
    public TimeSpan TotalDuration { get; private set; }
    public IReadOnlyList<CalendarDay> CalendarDays { get; private set; } = [];
    public IReadOnlyList<ProgramSummary> Programs { get; private set; } = [];
    public IReadOnlyList<TrainingDaySummary> TrainingDays { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var currentYear = timeProvider.GetUtcNow().Year;
        SelectedYear = Year is >= 2000 and <= 2100 ? Year.Value : currentYear;
        Year = SelectedYear;
        var from = new DateTimeOffset(SelectedYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddYears(1);

        var workouts = await database.WorkoutSessions
            .AsNoTracking()
            .Where(workout => workout.CompletedAt >= from && workout.CompletedAt < to)
            .OrderBy(workout => workout.CompletedAt)
            .ThenBy(workout => workout.Id)
            .Select(workout => new AnalyticsWorkout
            {
                ProgramName = workout.ProgramNameSnapshot,
                WorkoutName = workout.WorkoutNameSnapshot,
                StartedAt = workout.StartedAt,
                CompletedAt = workout.CompletedAt,
                ExerciseCount = workout.Exercises.Count,
                SetCount = workout.Exercises.SelectMany(exercise => exercise.Sets).Count(),
            })
            .ToListAsync(cancellationToken);

        TotalWorkouts = workouts.Count;
        TotalSets = workouts.Sum(workout => workout.SetCount);
        TotalDuration = TimeSpan.FromTicks(workouts.Sum(workout => workout.Duration.Ticks));

        var workoutCountsByDay = workouts
            .GroupBy(workout => DateOnly.FromDateTime(workout.CompletedAt.UtcDateTime))
            .ToDictionary(group => group.Key, group => group.Count());
        ActiveDays = workoutCountsByDay.Count;
        LongestStreak = CalculateLongestStreak(workoutCountsByDay.Keys);
        CalendarDays = BuildCalendar(SelectedYear, workoutCountsByDay);

        Programs = workouts
            .GroupBy(workout => workout.ProgramName)
            .Select(group => new ProgramSummary
            {
                ProgramName = group.Key,
                SessionCount = group.Count(),
                ActiveDays = group.Select(workout => workout.CompletedAt.UtcDateTime.Date).Distinct().Count(),
                AverageDuration = AverageDuration(group),
                ExerciseCount = group.Sum(workout => workout.ExerciseCount),
                SetCount = group.Sum(workout => workout.SetCount),
                LastPerformed = group.Max(workout => workout.CompletedAt),
            })
            .OrderByDescending(summary => summary.SessionCount)
            .ThenBy(summary => summary.ProgramName)
            .ToList();

        TrainingDays = workouts
            .GroupBy(workout => new { workout.ProgramName, workout.WorkoutName })
            .Select(group => new TrainingDaySummary
            {
                ProgramName = group.Key.ProgramName,
                WorkoutName = group.Key.WorkoutName,
                SessionCount = group.Count(),
                AverageDuration = AverageDuration(group),
                AverageSetCount = group.Average(workout => workout.SetCount),
                LastPerformed = group.Max(workout => workout.CompletedAt),
            })
            .OrderByDescending(summary => summary.SessionCount)
            .ThenBy(summary => summary.ProgramName)
            .ThenBy(summary => summary.WorkoutName)
            .ToList();
    }

    private static IReadOnlyList<CalendarDay> BuildCalendar(
        int year,
        IReadOnlyDictionary<DateOnly, int> workoutCounts)
    {
        var firstDay = new DateOnly(year, 1, 1);
        var lastDay = new DateOnly(year, 12, 31);
        var leadingDays = ((int)firstDay.DayOfWeek + 6) % 7;
        var trailingDays = 6 - (((int)lastDay.DayOfWeek + 6) % 7);
        var calendarStart = firstDay.AddDays(-leadingDays);
        var calendarEnd = lastDay.AddDays(trailingDays);
        var maximum = workoutCounts.Count == 0 ? 0 : workoutCounts.Values.Max();
        var days = new List<CalendarDay>();

        for (var date = calendarStart; date <= calendarEnd; date = date.AddDays(1))
        {
            var count = workoutCounts.GetValueOrDefault(date);
            days.Add(new CalendarDay
            {
                Date = date,
                WorkoutCount = count,
                Intensity = GetIntensity(count, maximum),
                IsInSelectedYear = date.Year == year,
            });
        }

        return days;
    }

    private static int GetIntensity(int count, int maximum)
    {
        if (count == 0 || maximum == 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(count * 4d / maximum), 1, 4);
    }

    private static int CalculateLongestStreak(IEnumerable<DateOnly> workoutDates)
    {
        var orderedDates = workoutDates.Order().ToArray();
        var longest = 0;
        var current = 0;
        DateOnly? previous = null;
        foreach (var date in orderedDates)
        {
            current = previous.HasValue && date == previous.Value.AddDays(1) ? current + 1 : 1;
            longest = Math.Max(longest, current);
            previous = date;
        }

        return longest;
    }

    private static TimeSpan AverageDuration(IEnumerable<AnalyticsWorkout> workouts)
    {
        var durations = workouts.Select(workout => workout.Duration.Ticks).ToArray();
        return durations.Length == 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks((long)durations.Average());
    }

    private sealed class AnalyticsWorkout
    {
        public required string ProgramName { get; init; }
        public required string WorkoutName { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
        public int ExerciseCount { get; init; }
        public int SetCount { get; init; }
        public TimeSpan Duration => CompletedAt - StartedAt;
    }

    public sealed class CalendarDay
    {
        public DateOnly Date { get; init; }
        public int WorkoutCount { get; init; }
        public int Intensity { get; init; }
        public bool IsInSelectedYear { get; init; }
    }

    public sealed class ProgramSummary
    {
        public required string ProgramName { get; init; }
        public int SessionCount { get; init; }
        public int ActiveDays { get; init; }
        public TimeSpan AverageDuration { get; init; }
        public int ExerciseCount { get; init; }
        public int SetCount { get; init; }
        public DateTimeOffset LastPerformed { get; init; }
    }

    public sealed class TrainingDaySummary
    {
        public required string ProgramName { get; init; }
        public required string WorkoutName { get; init; }
        public int SessionCount { get; init; }
        public TimeSpan AverageDuration { get; init; }
        public double AverageSetCount { get; init; }
        public DateTimeOffset LastPerformed { get; init; }
    }
}

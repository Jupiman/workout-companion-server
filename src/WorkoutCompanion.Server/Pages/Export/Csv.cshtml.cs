using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Pages.Export;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class CsvHistoryExportModel(WorkoutDbContext database)
    : HistoryExportPageModelBase(database)
{
    private static readonly string[] Header =
    [
        "workoutSyncId", "startedAt", "completedAt", "durationSeconds", "programName",
        "workoutName", "workoutStatus", "exerciseSyncId", "progressionTrackSyncId",
        "exerciseName", "exerciseSortOrder", "trackingMode", "setSyncId", "setOrder",
        "setType", "setStatus", "prescribedWeightCentiKg", "prescribedReps",
        "prescribedDurationSeconds", "actualWeightCentiKg", "actualReps",
        "actualDurationSeconds", "setCompletedAt",
    ];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var workouts = await LoadWorkoutsAsync(cancellationToken);
        var csv = new StringBuilder();
        AppendRow(csv, Header);
        foreach (var workout in workouts)
        {
            if (workout.Exercises.Count == 0)
            {
                AppendRow(csv, BuildRow(workout, null, null));
                continue;
            }

            foreach (var exercise in workout.Exercises)
            {
                if (exercise.Sets.Count == 0)
                {
                    AppendRow(csv, BuildRow(workout, exercise, null));
                    continue;
                }

                foreach (var set in exercise.Sets)
                {
                    AppendRow(csv, BuildRow(workout, exercise, set));
                }
            }
        }

        var body = Encoding.UTF8.GetBytes(csv.ToString());
        var preamble = Encoding.UTF8.GetPreamble();
        var content = new byte[preamble.Length + body.Length];
        preamble.CopyTo(content, 0);
        body.CopyTo(content, preamble.Length);
        return File(content, "text/csv; charset=utf-8", $"workout-history-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string?[] BuildRow(
        ExportWorkout workout,
        ExportExercise? exercise,
        ExportSet? set) =>
    [
        workout.SyncId.ToString("D"),
        workout.StartedAt.ToString("O", CultureInfo.InvariantCulture),
        workout.CompletedAt.ToString("O", CultureInfo.InvariantCulture),
        ((long)(workout.CompletedAt - workout.StartedAt).TotalSeconds).ToString(CultureInfo.InvariantCulture),
        ProtectFormula(workout.ProgramName),
        ProtectFormula(workout.WorkoutName),
        workout.Status.ToString().ToUpperInvariant(),
        exercise?.SyncId.ToString("D"),
        exercise?.ProgressionTrackSyncId?.ToString("D"),
        ProtectFormula(exercise?.Name),
        exercise?.SortOrder.ToString(CultureInfo.InvariantCulture),
        exercise?.TrackingMode.ToString().ToUpperInvariant(),
        set?.SyncId.ToString("D"),
        set?.SetOrder.ToString(CultureInfo.InvariantCulture),
        set?.SetType.ToString().ToUpperInvariant(),
        set?.Status.ToString().ToUpperInvariant(),
        set?.PrescribedWeightCentiKg?.ToString(CultureInfo.InvariantCulture),
        set?.PrescribedReps?.ToString(CultureInfo.InvariantCulture),
        set?.PrescribedDurationSeconds?.ToString(CultureInfo.InvariantCulture),
        set?.ActualWeightCentiKg?.ToString(CultureInfo.InvariantCulture),
        set?.ActualReps?.ToString(CultureInfo.InvariantCulture),
        set?.ActualDurationSeconds?.ToString(CultureInfo.InvariantCulture),
        set?.CompletedAt?.ToString("O", CultureInfo.InvariantCulture),
    ];

    private static void AppendRow(StringBuilder builder, IEnumerable<string?> values)
    {
        builder.AppendJoin(',', values.Select(Escape));
        builder.Append("\r\n");
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static string? ProtectFormula(string? value) =>
        value is { Length: > 0 } && value[0] is '=' or '+' or '-' or '@'
            ? $"'{value}"
            : value;
}

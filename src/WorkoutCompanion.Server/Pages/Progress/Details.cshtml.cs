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
public sealed class ProgressDetailsModel(WorkoutDbContext database) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateTo { get; set; }

    public ProgressTrackDetail? Track { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid trackSyncId, CancellationToken cancellationToken)
    {
        var header = await database.SessionExercises
            .AsNoTracking()
            .Where(exercise => exercise.SourceProgressionTrackSyncId == trackSyncId)
            .OrderByDescending(exercise => exercise.WorkoutSession.CompletedAt)
            .ThenByDescending(exercise => exercise.Id)
            .Select(exercise => new TrackHeader
            {
                ExerciseName = exercise.ExerciseNameSnapshot,
                ProgramName = exercise.WorkoutSession.ProgramNameSnapshot,
                TrackingMode = exercise.TrackingMode,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return NotFound();
        }

        var query = database.SessionExercises
            .AsNoTracking()
            .Where(exercise => exercise.SourceProgressionTrackSyncId == trackSyncId);

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

        var rawSessions = await query
            .OrderByDescending(exercise => exercise.WorkoutSession.CompletedAt)
            .ThenByDescending(exercise => exercise.WorkoutSessionId)
            .ThenBy(exercise => exercise.Id)
            .Select(exercise => new RawSession
            {
                WorkoutSessionId = exercise.WorkoutSessionId,
                WorkoutSyncId = exercise.WorkoutSession.SyncId,
                WorkoutName = exercise.WorkoutSession.WorkoutNameSnapshot,
                CompletedAt = exercise.WorkoutSession.CompletedAt,
                Sets = exercise.Sets
                    .Where(set => set.Status == WorkoutSetStatus.Completed
                        && set.SetType != WorkoutSetType.Warmup)
                    .OrderBy(set => set.SetOrder)
                    .ThenBy(set => set.Id)
                    .Select(set => new RawSet
                    {
                        ActualWeightCentiKg = set.ActualWeightCentiKg,
                        ActualReps = set.ActualReps,
                        ActualDurationSeconds = set.ActualDurationSeconds,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var entries = rawSessions
            .GroupBy(session => session.WorkoutSessionId)
            .Select(group => BuildEntry(group.First(), group.SelectMany(session => session.Sets)))
            .OrderByDescending(entry => entry.CompletedAt)
            .ThenByDescending(entry => entry.WorkoutSessionId)
            .ToList();
        var performedSets = rawSessions.SelectMany(session => session.Sets).ToList();
        var heaviestSet = performedSets
            .Where(set => set.ActualWeightCentiKg.HasValue)
            .OrderByDescending(set => set.ActualWeightCentiKg)
            .ThenByDescending(set => set.ActualReps)
            .FirstOrDefault();
        var repValues = performedSets
            .Where(set => set.ActualReps.HasValue)
            .Select(set => set.ActualReps!.Value)
            .ToList();
        var estimatedOneRepMaxValues = entries
            .Where(entry => entry.EstimatedOneRepMaxKg.HasValue)
            .Select(entry => entry.EstimatedOneRepMaxKg!.Value)
            .ToList();
        var ascendingEntries = entries.OrderBy(entry => entry.CompletedAt).ToList();

        Track = new ProgressTrackDetail
        {
            TrackSyncId = trackSyncId,
            ExerciseName = header.ExerciseName,
            ProgramName = header.ProgramName,
            TrackingMode = header.TrackingMode,
            Entries = entries,
            SessionCount = entries.Count,
            HeaviestSet = heaviestSet is null
                ? null
                : FormatWeightAndReps(heaviestSet.ActualWeightCentiKg, heaviestSet.ActualReps),
            BestReps = repValues.Count == 0 ? null : repValues.Max(),
            BestEstimatedOneRepMax = estimatedOneRepMaxValues.Count == 0
                ? null
                : estimatedOneRepMaxValues.Max(),
            WeightChart = ChartSeries.Create(ascendingEntries, entry => entry.WeightKg),
            RepsChart = ChartSeries.Create(ascendingEntries, entry => entry.BestReps),
            EstimatedOneRepMaxChart = ChartSeries.Create(
                ascendingEntries,
                entry => entry.EstimatedOneRepMaxKg),
        };

        return Page();
    }

    public static string FormatDecimal(decimal? value, string suffix = "") => value.HasValue
        ? $"{value.Value.ToString("0.##", CultureInfo.InvariantCulture)}{suffix}"
        : "—";

    public static string FormatDuration(int? seconds) => seconds.HasValue
        ? TimeSpan.FromSeconds(seconds.Value).ToString(@"m\:ss", CultureInfo.InvariantCulture)
        : "—";

    private static TrackSessionEntry BuildEntry(RawSession session, IEnumerable<RawSet> sourceSets)
    {
        var sets = sourceSets.ToList();
        var weightedSets = sets
            .Where(set => set.ActualWeightCentiKg.HasValue && set.ActualReps.HasValue)
            .ToList();
        var weightKg = sets
            .Where(set => set.ActualWeightCentiKg.HasValue)
            .Select(set => set.ActualWeightCentiKg!.Value / 100m)
            .DefaultIfEmpty()
            .Max();
        var estimatedOneRepMax = weightedSets
            .Select(set => EstimateOneRepMax(set.ActualWeightCentiKg!.Value, set.ActualReps!.Value))
            .DefaultIfEmpty()
            .Max();
        var volume = weightedSets.Sum(set =>
            set.ActualWeightCentiKg!.Value / 100m * set.ActualReps!.Value);
        var reps = sets
            .Where(set => set.ActualReps.HasValue)
            .Select(set => set.ActualReps!.Value)
            .ToList();
        var duration = sets
            .Where(set => set.ActualDurationSeconds.HasValue)
            .Select(set => set.ActualDurationSeconds!.Value)
            .DefaultIfEmpty()
            .Max();

        return new TrackSessionEntry
        {
            WorkoutSessionId = session.WorkoutSessionId,
            WorkoutSyncId = session.WorkoutSyncId,
            WorkoutName = session.WorkoutName,
            CompletedAt = session.CompletedAt,
            WeightKg = sets.Any(set => set.ActualWeightCentiKg.HasValue) ? weightKg : null,
            RepsSummary = reps.Count == 0 ? "—" : string.Join('/', reps),
            BestReps = reps.Count == 0 ? null : reps.Max(),
            BestDurationSeconds = sets.Any(set => set.ActualDurationSeconds.HasValue) ? duration : null,
            VolumeKg = weightedSets.Count == 0 ? null : volume,
            EstimatedOneRepMaxKg = weightedSets.Count == 0 ? null : estimatedOneRepMax,
            SetCount = sets.Count,
        };
    }

    private static decimal EstimateOneRepMax(int weightCentiKg, int reps) =>
        decimal.Round(weightCentiKg / 100m * (1m + reps / 30m), 2);

    private static string FormatWeightAndReps(int? weightCentiKg, int? reps) =>
        $"{FormatDecimal(weightCentiKg / 100m, " kg")} × {reps?.ToString(CultureInfo.InvariantCulture) ?? "—"}";

    private static DateTimeOffset ToUtcStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private sealed class TrackHeader
    {
        public required string ExerciseName { get; init; }
        public required string ProgramName { get; init; }
        public TrackingMode TrackingMode { get; init; }
    }

    private sealed class RawSession
    {
        public long WorkoutSessionId { get; init; }
        public Guid WorkoutSyncId { get; init; }
        public required string WorkoutName { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
        public required IReadOnlyList<RawSet> Sets { get; init; }
    }

    private sealed class RawSet
    {
        public int? ActualWeightCentiKg { get; init; }
        public int? ActualReps { get; init; }
        public int? ActualDurationSeconds { get; init; }
    }

    public sealed class ProgressTrackDetail
    {
        public Guid TrackSyncId { get; init; }
        public required string ExerciseName { get; init; }
        public required string ProgramName { get; init; }
        public TrackingMode TrackingMode { get; init; }
        public required IReadOnlyList<TrackSessionEntry> Entries { get; init; }
        public int SessionCount { get; init; }
        public string? HeaviestSet { get; init; }
        public int? BestReps { get; init; }
        public decimal? BestEstimatedOneRepMax { get; init; }
        public required ChartSeries WeightChart { get; init; }
        public required ChartSeries RepsChart { get; init; }
        public required ChartSeries EstimatedOneRepMaxChart { get; init; }
    }

    public sealed class TrackSessionEntry
    {
        public long WorkoutSessionId { get; init; }
        public Guid WorkoutSyncId { get; init; }
        public required string WorkoutName { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
        public decimal? WeightKg { get; init; }
        public required string RepsSummary { get; init; }
        public int? BestReps { get; init; }
        public int? BestDurationSeconds { get; init; }
        public decimal? VolumeKg { get; init; }
        public decimal? EstimatedOneRepMaxKg { get; init; }
        public int SetCount { get; init; }
    }

    public sealed class ChartSeries
    {
        public required string Points { get; init; }
        public required IReadOnlyList<ChartMarker> Markers { get; init; }
        public decimal? Minimum { get; init; }
        public decimal? Maximum { get; init; }
        public string? FirstDate { get; init; }
        public string? LastDate { get; init; }
        public bool HasData => Points.Length > 0;

        public static ChartSeries Create(
            IReadOnlyList<TrackSessionEntry> entries,
            Func<TrackSessionEntry, decimal?> selector)
        {
            var values = entries.Select(selector).ToArray();
            var present = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
            if (present.Length == 0)
            {
                return new ChartSeries { Points = string.Empty, Markers = [] };
            }

            const decimal width = 700m;
            const decimal height = 170m;
            const decimal inset = 10m;
            var minimum = present.Min();
            var maximum = present.Max();
            var range = maximum - minimum;
            var markers = values
                .Select((value, index) => new { value, index })
                .Where(point => point.value.HasValue)
                .Select(point =>
                {
                    var x = values.Length == 1
                        ? width / 2m
                        : inset + point.index * (width - (2m * inset)) / (values.Length - 1m);
                    var y = range == 0m
                        ? height / 2m
                        : inset + (maximum - point.value!.Value) * (height - (2m * inset)) / range;
                    return new ChartMarker
                    {
                        X = x.ToString("0.##", CultureInfo.InvariantCulture),
                        Y = y.ToString("0.##", CultureInfo.InvariantCulture),
                    };
                })
                .ToList();

            return new ChartSeries
            {
                Points = string.Join(' ', markers.Select(marker => $"{marker.X},{marker.Y}")),
                Markers = markers,
                Minimum = minimum,
                Maximum = maximum,
                FirstDate = entries.First().CompletedAt.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture),
                LastDate = entries.Last().CompletedAt.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture),
            };
        }
    }

    public sealed class ChartMarker
    {
        public required string X { get; init; }
        public required string Y { get; init; }
    }
}

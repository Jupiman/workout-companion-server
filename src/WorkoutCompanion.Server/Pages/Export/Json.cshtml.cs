using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkoutCompanion.Server.Data;

namespace WorkoutCompanion.Server.Pages.Export;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class JsonHistoryExportModel(WorkoutDbContext database)
    : HistoryExportPageModelBase(database)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var workouts = await LoadWorkoutsAsync(cancellationToken);
        var content = JsonSerializer.SerializeToUtf8Bytes(workouts, JsonOptions);
        return File(content, "application/json; charset=utf-8", $"workout-history-{DateTime.UtcNow:yyyyMMdd}.json");
    }
}

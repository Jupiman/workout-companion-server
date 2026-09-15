using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Auth;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;
using WorkoutCompanion.Server.Ingestion;

if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    try
    {
        using var response = await client.GetAsync("http://localhost:8080/health");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        return 1;
    }
    catch (TaskCanceledException)
    {
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffK ");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 2 * 1024 * 1024);

var dataDirectory = builder.Configuration["Workout:DataDirectory"]
    ?? Environment.GetEnvironmentVariable("WORKOUT_DATA_DIRECTORY")
    ?? Path.Combine(builder.Environment.ContentRootPath, "runtime-data");
Directory.CreateDirectory(dataDirectory);
builder.Services.AddDataProtection()
    .SetApplicationName("WorkoutCompanion.Server")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "data-protection")));

builder.Services
    .AddRazorPages()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services
    .AddAuthentication()
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "workout-companion-session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    })
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
        BearerTokenAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<ServerTokenService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<WorkoutPayloadValidator>();
builder.Services.AddScoped<WorkoutIngestionService>();

var connectionString = builder.Configuration.GetConnectionString("WorkoutCompanion");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(dataDirectory, "workout-companion.db"),
        ForeignKeys = true,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
    }.ToString();
}

builder.Services.AddDbContext<WorkoutDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("UnhandledException");
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        logger.LogError(exception, "Unhandled server error for {Method} {Path}", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ApiError("SERVER_ERROR", "An unexpected server error occurred."));
    });
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

var api = app.MapGroup("/api/v1")
    .RequireAuthorization(new AuthorizeAttribute
    {
        AuthenticationSchemes = BearerTokenAuthenticationHandler.SchemeName,
    });

api.MapGet("/info", () => Results.Ok(new ServerInfoResponse(
    ServerVersion: typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.1.0",
    ApiVersion: 1,
    MinimumPayloadSchemaVersion: 1,
    MaximumPayloadSchemaVersion: 1)));

api.MapPut("/workouts/{workoutSyncId:guid}", async (
    Guid workoutSyncId,
    WorkoutUploadRequest request,
    WorkoutPayloadValidator validator,
    WorkoutIngestionService ingestion,
    CancellationToken cancellationToken) =>
{
    var errors = validator.Validate(request, workoutSyncId);
    if (errors.Count > 0)
    {
        return Results.Json(
            new ApiError("INVALID_PAYLOAD", errors[0], errors),
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    var result = await ingestion.UpsertAsync(request, cancellationToken);
    var response = new WorkoutUploadResult(request.SyncId, result.ToString().ToUpperInvariant());
    return result == IngestionOutcome.Created
        ? Results.Created($"/api/v1/workouts/{request.SyncId:D}", response)
        : Results.Ok(response);
});

api.MapPost("/workouts/batch", async (
    BatchWorkoutUploadRequest request,
    WorkoutPayloadValidator validator,
    WorkoutIngestionService ingestion,
    CancellationToken cancellationToken) =>
{
    if (request.Workouts is null)
    {
        return Results.BadRequest(new ApiError("INVALID_PAYLOAD", "workouts is required."));
    }

    if (request.Workouts.Count > WorkoutPayloadValidator.MaximumBatchSize)
    {
        return Results.Json(
            new ApiError("BATCH_TOO_LARGE", $"A batch may contain at most {WorkoutPayloadValidator.MaximumBatchSize} workouts."),
            statusCode: StatusCodes.Status413PayloadTooLarge);
    }

    var results = new List<BatchWorkoutUploadResult>(request.Workouts.Count);
    foreach (var workout in request.Workouts)
    {
        if (workout is null)
        {
            results.Add(new BatchWorkoutUploadResult(Guid.Empty, "REJECTED", "A batch item must not be null."));
            continue;
        }

        var errors = validator.Validate(workout);
        if (errors.Count > 0)
        {
            results.Add(new BatchWorkoutUploadResult(workout.SyncId, "REJECTED", errors[0], errors));
            continue;
        }

        try
        {
            var outcome = await ingestion.UpsertAsync(workout, cancellationToken);
            results.Add(new BatchWorkoutUploadResult(
                workout.SyncId,
                outcome.ToString().ToUpperInvariant()));
        }
        catch (DbUpdateException exception)
        {
            app.Logger.LogWarning(exception, "Rejected workout {WorkoutSyncId} during batch persistence", workout.SyncId);
            results.Add(new BatchWorkoutUploadResult(
                workout.SyncId,
                "REJECTED",
                "The workout could not be persisted."));
        }
    }

    return Results.Ok(new BatchWorkoutUploadResponse(results));
});

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
    await database.Database.MigrateAsync();
    _ = scope.ServiceProvider.GetRequiredService<ServerTokenService>();
}

await app.RunAsync();
return 0;

public partial class Program;

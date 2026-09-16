using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutCompanion.Server.Contracts;
using WorkoutCompanion.Server.Data;
using WorkoutCompanion.Server.Data.Entities;

namespace WorkoutCompanion.Server.Tests;

public sealed class HistoryRouteTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task History_workout_and_progress_routes_require_cookie_authentication()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var history = await client.GetAsync("/history");
        using var workout = await client.GetAsync($"/workouts/{Guid.NewGuid():D}");
        using var progress = await client.GetAsync("/progress");

        Assert.Equal(HttpStatusCode.Redirect, history.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, workout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, progress.StatusCode);
        Assert.Equal("/Login", history.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Authenticated_history_and_workout_detail_routes_render()
    {
        var workoutId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            database.WorkoutSessions.Add(CreateWorkout(workoutId));
            await database.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        await LoginAsync(client);

        using var history = await client.GetAsync("/history");
        using var detail = await client.GetAsync($"/workouts/{workoutId:D}");
        var historyHtml = await history.Content.ReadAsStringAsync();
        var detailHtml = await detail.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains("Route Workout", historyHtml, StringComparison.Ordinal);
        Assert.Contains("Route Workout", detailHtml, StringComparison.Ordinal);
        Assert.Contains("Warm-up", detailHtml, StringComparison.Ordinal);
        Assert.Contains(">-1<", detailHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task History_pagination_links_preserve_filter_state_and_highlighting()
    {
        var completedAt = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var workouts = Enumerable.Range(0, 26)
            .Select(index =>
            {
                var workout = CreateWorkout(Guid.NewGuid());
                workout.ProgramNameSnapshot = "Paging Program";
                workout.WorkoutNameSnapshot = $"Paging Workout {index:00}";
                workout.StartedAt = completedAt.AddMinutes(index).AddHours(-1);
                workout.CompletedAt = completedAt.AddMinutes(index);
                workout.ReceivedAt = completedAt.AddMinutes(index + 1);
                workout.LastReceivedAt = completedAt.AddMinutes(index + 1);
                return workout;
            });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            database.WorkoutSessions.AddRange(workouts);
            await database.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        await LoginAsync(client);

        using var response = await client.GetAsync(
            "/history?Program=Paging%20Program&WorkoutName=Paging&Status=Completed&Exercise=Bench%20Press&Search=Paging&DateFrom=2026-09-01&DateTo=2026-09-30");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("Page 1 of ([2-9]|[1-9][0-9]+)", html);
        Assert.Contains("<mark>Paging</mark>", html, StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", html, StringComparison.Ordinal);
        Assert.Contains("dateFrom=", html, StringComparison.Ordinal);
        Assert.Contains("dateTo=", html, StringComparison.Ordinal);
        Assert.Contains("program=Paging%20Program", html, StringComparison.Ordinal);
        Assert.Contains("workoutName=Paging", html, StringComparison.Ordinal);
        Assert.Contains("status=Completed", html, StringComparison.Ordinal);
        Assert.Contains("exercise=Bench%20Press", html, StringComparison.Ordinal);
        Assert.Contains("search=Paging", html, StringComparison.Ordinal);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        using var loginPage = await client.GetAsync("/Login");
        loginPage.EnsureSuccessStatusCode();
        var html = await loginPage.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The login page did not contain an antiforgery token.");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Token"] = TestApplicationFactory.ApiToken,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value),
        });

        using var response = await client.PostAsync("/Login", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static WorkoutSessionEntity CreateWorkout(Guid workoutId)
    {
        var completedAt = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        return new WorkoutSessionEntity
        {
            SyncId = workoutId,
            ProgramNameSnapshot = "Route Program",
            WorkoutNameSnapshot = "Route Workout",
            StartedAt = completedAt.AddHours(-1),
            CompletedAt = completedAt,
            Status = WorkoutStatus.Completed,
            PayloadSchemaVersion = 1,
            ReceivedAt = completedAt.AddMinutes(1),
            LastReceivedAt = completedAt.AddMinutes(1),
            Exercises =
            [
                new SessionExerciseEntity
                {
                    SyncId = Guid.NewGuid(),
                    SourceProgressionTrackSyncId = Guid.NewGuid(),
                    ExerciseNameSnapshot = "Bench Press",
                    SortOrderSnapshot = 0,
                    TrackingMode = TrackingMode.WeightReps,
                    Sets =
                    [
                        new SessionSetEntity
                        {
                            SyncId = Guid.NewGuid(),
                            SetOrder = -1,
                            SetType = WorkoutSetType.Warmup,
                            Status = WorkoutSetStatus.Completed,
                            IsPlanned = true,
                            PrescribedWeightCentiKg = 5000,
                            PrescribedReps = 5,
                            ActualWeightCentiKg = 5000,
                            ActualReps = 5,
                            CompletedAt = completedAt.AddMinutes(-30),
                        },
                        new SessionSetEntity
                        {
                            SyncId = Guid.NewGuid(),
                            SetOrder = 0,
                            SetType = WorkoutSetType.Working,
                            Status = WorkoutSetStatus.Completed,
                            IsPlanned = true,
                            CountsForProgression = true,
                            PrescribedWeightCentiKg = 7000,
                            PrescribedReps = 8,
                            ActualWeightCentiKg = 7000,
                            ActualReps = 8,
                            CompletedAt = completedAt.AddMinutes(-20),
                        },
                    ],
                },
            ],
        };
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WorkoutCompanion.Server.Tests;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    public const string ApiToken = "integration-test-token-with-enough-entropy";
    private readonly string testDirectory = Path.Combine(Path.GetTempPath(), $"workout-companion-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(testDirectory);
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workout:ApiToken"] = ApiToken,
                ["Workout:DataDirectory"] = testDirectory,
                ["ConnectionStrings:WorkoutCompanion"] = $"Data Source={Path.Combine(testDirectory, "test.db")};Foreign Keys=True",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !Directory.Exists(testDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(testDirectory, recursive: true);
        }
        catch (IOException)
        {
            // SQLite can release its final file handle shortly after the host shuts down.
        }
        catch (UnauthorizedAccessException)
        {
            // Test cleanup must not hide the actual test result.
        }
    }
}

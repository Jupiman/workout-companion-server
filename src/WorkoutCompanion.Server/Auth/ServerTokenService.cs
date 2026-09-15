using System.Security.Cryptography;
using System.Text;

namespace WorkoutCompanion.Server.Auth;

public sealed class ServerTokenService
{
    private const int GeneratedTokenBytes = 48;
    private readonly byte[] tokenHash;

    public ServerTokenService(IConfiguration configuration, IWebHostEnvironment environment, ILogger<ServerTokenService> logger)
    {
        var configuredToken = configuration["Workout:ApiToken"]
            ?? Environment.GetEnvironmentVariable("WORKOUT_API_TOKEN");

        if (!string.IsNullOrWhiteSpace(configuredToken))
        {
            tokenHash = Hash(configuredToken);
            logger.LogInformation("Using the API token supplied by server configuration.");
            return;
        }

        var dataDirectory = configuration["Workout:DataDirectory"]
            ?? Environment.GetEnvironmentVariable("WORKOUT_DATA_DIRECTORY")
            ?? Path.Combine(environment.ContentRootPath, "runtime-data");
        Directory.CreateDirectory(dataDirectory);
        var tokenHashPath = Path.Combine(dataDirectory, "token.sha256");

        if (File.Exists(tokenHashPath))
        {
            tokenHash = Convert.FromBase64String(File.ReadAllText(tokenHashPath).Trim());
            if (tokenHash.Length != SHA256.HashSizeInBytes)
            {
                throw new InvalidOperationException($"The persisted token hash at '{tokenHashPath}' is invalid.");
            }

            logger.LogInformation("Using the persisted API token hash from {TokenHashPath}.", tokenHashPath);
            return;
        }

        var generatedToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(GeneratedTokenBytes));
        tokenHash = Hash(generatedToken);
        File.WriteAllText(tokenHashPath, Convert.ToBase64String(tokenHash));

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(tokenHashPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        logger.LogWarning(
            "No API token was configured. Generated first-start token (shown once): {GeneratedToken}",
            generatedToken);
    }

    public bool Validate(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var candidateHash = Hash(candidate);
        return CryptographicOperations.FixedTimeEquals(tokenHash, candidateHash);
    }

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}

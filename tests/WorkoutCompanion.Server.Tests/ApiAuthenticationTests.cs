using System.Net;
using System.Net.Http.Headers;

namespace WorkoutCompanion.Server.Tests;

public sealed class ApiAuthenticationTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Info_with_valid_token_succeeds()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApplicationFactory.ApiToken);

        using var response = await client.GetAsync("/api/v1/info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-token")]
    public async Task Info_without_valid_token_is_unauthorized(string? token)
    {
        using var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.GetAsync("/api/v1/info");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("UNAUTHORIZED", body, StringComparison.Ordinal);
    }
}


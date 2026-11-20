using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Tests;

public sealed class BootstrapAdminTests
{
    [Fact]
    public async Task ConfiguredPasswordSyncUpdatesExistingAdminAndRevokesSessions()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aeroguard-bootstrap-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            string originalToken;
            using (var initialFactory = new AeroGuardApiFactory(
                temporaryDirectory,
                "Initial-Admin-Password-42!",
                false))
            using (var initialClient = initialFactory.CreateClient())
            {
                originalToken = await LoginAsync(
                    initialClient,
                    "Initial-Admin-Password-42!");
            }

            using var restartedFactory = new AeroGuardApiFactory(
                temporaryDirectory,
                "Replacement-Admin-Password-84!",
                true);
            using var restartedClient = restartedFactory.CreateClient();
            using var oldPassword = await restartedClient.PostAsJsonAsync(
                "/api/auth/login",
                new { username = "admin", password = "Initial-Admin-Password-42!" },
                TestContext.Current.CancellationToken);
            var replacementToken = await LoginAsync(
                restartedClient,
                "Replacement-Admin-Password-84!");
            using var revokedSession = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            revokedSession.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                originalToken);
            using var revokedSessionResponse = await restartedClient.SendAsync(
                revokedSession,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
            Assert.NotEmpty(replacementToken);
            Assert.Equal(HttpStatusCode.Unauthorized, revokedSessionResponse.StatusCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    private static async Task<string> LoginAsync(HttpClient client, string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "admin", password },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return body.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Login returned no access token.");
    }
}

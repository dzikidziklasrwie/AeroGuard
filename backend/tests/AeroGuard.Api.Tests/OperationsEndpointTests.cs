using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AeroGuard.Api.Identity;
using AeroGuard.Api.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace AeroGuard.Api.Tests;

public sealed class OperationsEndpointTests(AeroGuardApiFactory factory)
    : IClassFixture<AeroGuardApiFactory>
{
    private static readonly CancellationToken CancellationToken = TestContext.Current.CancellationToken;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OperatorPlacesOwnedAircraftAndReceivesSubsystemTelemetry()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("flight-operator");
        await CreateUserAsync(admin.Token, username, "Operator");
        var operation = await LoginAsync(username, "Account-Password-42!");
        var aircraft = await CreateAircraftAsync(operation.Token, UniqueTail());
        var runId = await GetRunIdAsync("simulated-overheating");

        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            operation.Token,
            FlightRequest(aircraft.GetProperty("id").GetInt64(), runId, "WORLD", "WORLD"));
        var flight = await ReadJsonAsync(placement);
        using var visible = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/operations/flights",
            operation.Token);
        var flights = await ReadJsonAsync(visible);

        Assert.True(
            placement.StatusCode == HttpStatusCode.Created,
            await placement.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal("simulated-overheating", flight.GetProperty("scenario").GetString());
        Assert.Equal(6, flight.GetProperty("telemetry").GetArrayLength());
        Assert.Equal("TRANSIT", flight.GetProperty("route_phase").GetString());
        Assert.InRange(flight.GetProperty("route_progress").GetDouble(), 0.0, 1.0);
        Assert.True(flight.GetProperty("can_manage").GetBoolean());
        Assert.True(flight.GetProperty("can_remove").GetBoolean());
        Assert.Single(flights.EnumerateArray());
    }

    [Fact]
    public async Task ViewerSeesAssignedFlightButCannotPlaceOrReconfigureIt()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("flight-viewer");
        var viewerUser = await CreateUserAsync(admin.Token, username, "Viewer");
        var aircraft = await CreateAircraftAsync(admin.Token, UniqueTail());
        var aircraftId = aircraft.GetProperty("id").GetInt64();
        await AssignAsync(
            admin.Token,
            aircraftId,
            viewerUser.GetProperty("id").GetInt64(),
            "VIEWER");
        var runId = await GetRunIdAsync("normal-operation");
        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            admin.Token,
            FlightRequest(aircraftId, runId, "CONTINENT", "EU"));
        var flight = await ReadJsonAsync(placement);
        var viewer = await LoginAsync(username, "Account-Password-42!");

        using var visible = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/operations/flights",
            viewer.Token);
        var flights = await ReadJsonAsync(visible);
        using var place = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            viewer.Token,
            FlightRequest(aircraftId, runId, "CONTINENT", "EU"));
        using var update = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/operations/flights/{flight.GetProperty("id").GetInt64()}",
            viewer.Token,
            FlightUpdate(aircraftId, runId, flight.GetProperty("version").GetInt64()));

        Assert.Single(flights.EnumerateArray());
        Assert.False(flights[0].GetProperty("can_manage").GetBoolean());
        Assert.False(flights[0].GetProperty("can_remove").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, place.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    [Fact]
    public async Task PermissionOverrideCanStopOwnerFromRemovingFlight()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("restricted-owner");
        var user = await CreateUserAsync(admin.Token, username, "Operator");
        var operation = await LoginAsync(username, "Account-Password-42!");
        var aircraft = await CreateAircraftAsync(operation.Token, UniqueTail());
        var aircraftId = aircraft.GetProperty("id").GetInt64();
        var runId = await GetRunIdAsync("normal-operation");
        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            operation.Token,
            FlightRequest(aircraftId, runId, "COUNTRY", "POL"));
        var flight = await ReadJsonAsync(placement);
        using var permissions = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/admin/users/{user.GetProperty("id").GetInt64()}/permissions",
            admin.Token,
            new { permissions = new[] { "CHAT_SEND" } });
        using var remove = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/operations/flights/{flight.GetProperty("id").GetInt64()}",
            operation.Token);

        Assert.Equal(HttpStatusCode.OK, permissions.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);
    }

    [Fact]
    public async Task ExplicitEmptyPermissionOverrideRemovesOperatorDefaults()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("empty-permissions");
        var user = await CreateUserAsync(admin.Token, username, "Operator");
        var operation = await LoginAsync(username, "Account-Password-42!");
        var aircraft = await CreateAircraftAsync(operation.Token, UniqueTail());
        var runId = await GetRunIdAsync("normal-operation");
        using var replace = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/admin/users/{user.GetProperty("id").GetInt64()}/permissions",
            admin.Token,
            new { permissions = Array.Empty<string>() });
        using var current = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/operations/permissions",
            operation.Token);
        var currentBody = await ReadJsonAsync(current);
        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            operation.Token,
            FlightRequest(
                aircraft.GetProperty("id").GetInt64(),
                runId,
                "WORLD",
                "WORLD"));

        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        Assert.Empty(currentBody.GetProperty("permissions").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, placement.StatusCode);
    }

    [Fact]
    public async Task AdministratorReconfiguresAndRemovesAnotherOwnersFlight()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("remote-owner");
        await CreateUserAsync(admin.Token, username, "Operator");
        var operation = await LoginAsync(username, "Account-Password-42!");
        var aircraft = await CreateAircraftAsync(operation.Token, UniqueTail());
        var aircraftId = aircraft.GetProperty("id").GetInt64();
        var initialRunId = await GetRunIdAsync("normal-operation");
        var replacementRunId = await GetRunIdAsync("multiple-subsystem-failures");
        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            operation.Token,
            FlightRequest(aircraftId, initialRunId, "WORLD", "WORLD"));
        var flight = await ReadJsonAsync(placement);
        var flightId = flight.GetProperty("id").GetInt64();

        using var update = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/operations/flights/{flightId}",
            admin.Token,
            FlightUpdate(aircraftId, replacementRunId, flight.GetProperty("version").GetInt64()));
        var updated = await ReadJsonAsync(update);
        using var remove = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/operations/flights/{flightId}",
            admin.Token);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("multiple-subsystem-failures", updated.GetProperty("scenario").GetString());
        Assert.Equal(username, updated.GetProperty("owner_username").GetString());
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
    }

    [Fact]
    public async Task ActiveFlightMustBeRemovedBeforeAircraftDeletion()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var aircraft = await CreateAircraftAsync(admin.Token, UniqueTail());
        var aircraftId = aircraft.GetProperty("id").GetInt64();
        var runId = await GetRunIdAsync("normal-operation");
        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            admin.Token,
            FlightRequest(aircraftId, runId, "WORLD", "WORLD"));
        var flight = await ReadJsonAsync(placement);

        using var blockedDelete = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/aircraft/{aircraftId}",
            admin.Token);
        using var blockedStatus = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/aircraft/{aircraftId}",
            admin.Token,
            new
            {
                tail_number = aircraft.GetProperty("tail_number").GetString(),
                display_name = aircraft.GetProperty("display_name").GetString(),
                model = aircraft.GetProperty("model").GetString(),
                status = "MAINTENANCE",
                notes = aircraft.GetProperty("notes").GetString(),
                expected_version = aircraft.GetProperty("version").GetInt64(),
            });
        using var removeFlight = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/operations/flights/{flight.GetProperty("id").GetInt64()}",
            admin.Token);
        using var deleteAircraft = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/aircraft/{aircraftId}",
            admin.Token);

        Assert.Equal(HttpStatusCode.Conflict, blockedDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, blockedStatus.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removeFlight.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteAircraft.StatusCode);
    }

    [Fact]
    public async Task RemovingAssignmentImmediatelyRevokesFlightVisibility()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var viewerName = Unique("revoked-viewer");
        var viewerUser = await CreateUserAsync(admin.Token, viewerName, "Viewer");
        var viewer = await LoginAsync(viewerName, "Account-Password-42!");
        var aircraft = await CreateAircraftAsync(admin.Token, UniqueTail());
        var aircraftId = aircraft.GetProperty("id").GetInt64();
        var viewerId = viewerUser.GetProperty("id").GetInt64();
        await AssignAsync(admin.Token, aircraftId, viewerId, "VIEWER");
        var runId = await GetRunIdAsync("normal-operation");
        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            admin.Token,
            FlightRequest(aircraftId, runId, "CONTINENT", "EU"));

        using var visibleBefore = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/operations/flights",
            viewer.Token);
        var flightsBefore = await ReadJsonAsync(visibleBefore);
        using var removeAssignment = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/aircraft/{aircraftId}/assignments/{viewerId}",
            admin.Token);
        using var visibleAfter = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/operations/flights",
            viewer.Token);
        var flightsAfter = await ReadJsonAsync(visibleAfter);

        Assert.Single(flightsBefore.EnumerateArray());
        Assert.Equal(HttpStatusCode.NoContent, removeAssignment.StatusCode);
        Assert.Empty(flightsAfter.EnumerateArray());
    }

    [Fact]
    public async Task FlightChatSharesServerGeneratedPositionAndTelemetry()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var ownerName = Unique("chat-owner");
        await CreateUserAsync(admin.Token, ownerName, "Operator");
        var owner = await LoginAsync(ownerName, "Account-Password-42!");
        var viewerName = Unique("chat-viewer");
        var viewerUser = await CreateUserAsync(admin.Token, viewerName, "Viewer");
        var aircraft = await CreateAircraftAsync(owner.Token, UniqueTail());
        var aircraftId = aircraft.GetProperty("id").GetInt64();
        await AssignAsync(
            admin.Token,
            aircraftId,
            viewerUser.GetProperty("id").GetInt64(),
            "VIEWER");
        var viewer = await LoginAsync(viewerName, "Account-Password-42!");
        var runId = await GetRunIdAsync("power-fluctuations");
        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            owner.Token,
            FlightRequest(aircraftId, runId, "CONTINENT", "EU"));
        var flight = await ReadJsonAsync(placement);
        var flightId = flight.GetProperty("id").GetInt64();

        using var text = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            $"/api/operations/flights/{flightId}/messages",
            viewer.Token,
            new { message = "Monitoring from Warsaw.", share_type = "TEXT" });
        using var viewerTelemetry = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            $"/api/operations/flights/{flightId}/messages",
            viewer.Token,
            new { message = "", share_type = "TELEMETRY" });
        using var position = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            $"/api/operations/flights/{flightId}/messages",
            owner.Token,
            new { message = "", share_type = "LOCATION" });
        var positionBody = await ReadJsonAsync(position);
        using var telemetry = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            $"/api/operations/flights/{flightId}/messages",
            owner.Token,
            new { message = "Subsystem snapshot", share_type = "TELEMETRY" });
        var telemetryBody = await ReadJsonAsync(telemetry);
        using var tooLong = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            $"/api/operations/flights/{flightId}/messages",
            owner.Token,
            new { message = new string('x', 501), share_type = "TEXT" });
        using var messages = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/operations/flights/{flightId}/messages",
            viewer.Token);
        var messageList = await ReadJsonAsync(messages);

        Assert.Equal(HttpStatusCode.Created, text.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewerTelemetry.StatusCode);
        Assert.True(positionBody.GetProperty("payload").TryGetProperty("latitude", out _));
        Assert.Equal(6, telemetryBody.GetProperty("payload").GetProperty("telemetry").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(3, messageList.GetArrayLength());
    }

    [Fact]
    public async Task UnassignedAccountCannotReadAnotherFlightsChat()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var aircraft = await CreateAircraftAsync(admin.Token, UniqueTail());
        var runId = await GetRunIdAsync("normal-operation");
        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            admin.Token,
            FlightRequest(
                aircraft.GetProperty("id").GetInt64(),
                runId,
                "WORLD",
                "WORLD"));
        var flight = await ReadJsonAsync(placement);
        var username = Unique("unassigned-chat");
        await CreateUserAsync(admin.Token, username, "Viewer");
        var viewer = await LoginAsync(username, "Account-Password-42!");

        using var messages = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/operations/flights/{flight.GetProperty("id").GetInt64()}/messages",
            viewer.Token);

        Assert.Equal(HttpStatusCode.NotFound, messages.StatusCode);
    }

    [Fact]
    public async Task FlightUpdateRejectsStaleVersionAndInvalidRoute()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var aircraft = await CreateAircraftAsync(admin.Token, UniqueTail());
        var aircraftId = aircraft.GetProperty("id").GetInt64();
        var runId = await GetRunIdAsync("normal-operation");
        using var invalid = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            admin.Token,
            FlightRequest(aircraftId, runId, "COUNTRY", "XX"));
        using var unknownCountry = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            admin.Token,
            FlightRequest(aircraftId, runId, "COUNTRY", "ZZZ"));
        using var placement = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/operations/flights",
            admin.Token,
            FlightRequest(aircraftId, runId, "COUNTRY", "POL"));
        var flight = await ReadJsonAsync(placement);
        var flightId = flight.GetProperty("id").GetInt64();
        var version = flight.GetProperty("version").GetInt64();
        using var update = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/operations/flights/{flightId}",
            admin.Token,
            FlightUpdate(aircraftId, runId, version));
        using var stale = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/operations/flights/{flightId}",
            admin.Token,
            FlightUpdate(aircraftId, runId, version));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownCountry.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task LiveHubRequiresAnAuthenticatedSession()
    {
        using var anonymous = await _client.PostAsync(
            "/hubs/operations/negotiate?negotiateVersion=1",
            null,
            CancellationToken);
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        using var authenticated = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/hubs/operations/negotiate?negotiateVersion=1",
            admin.Token);
        using var queryAuthenticated = await _client.PostAsync(
            $"/hubs/operations/negotiate?negotiateVersion=1&access_token={admin.Token}",
            null,
            CancellationToken);
        using var queryOutsideHub = await _client.GetAsync(
            $"/api/aircraft?access_token={admin.Token}",
            CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, queryAuthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, queryOutsideHub.StatusCode);
    }

    [Fact]
    public async Task RevokedSessionIsRemovedFromLiveUpdateRecipients()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var identity = factory.Services.GetRequiredService<IIdentityRepository>();
        var tokenIssuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
        var registry = factory.Services.GetRequiredService<OperationsConnectionRegistry>();
        var activeAdministrators = await identity.GetActiveUserIdsInRoleAsync(
            BuiltInRoles.Admin,
            CancellationToken);
        Assert.Contains(admin.UserId, activeAdministrators);
        var session = await identity.FindSessionAsync(
            tokenIssuer.Hash(admin.Token),
            DateTimeOffset.UtcNow,
            CancellationToken);
        Assert.NotNull(session);
        var connectionId = $"test-{Guid.NewGuid():N}";
        registry.Register(connectionId, admin.UserId, session.Id);

        var before = await registry.GetActiveConnectionIdsAsync(
            [admin.UserId],
            CancellationToken);
        using var logout = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/auth/logout",
            admin.Token);
        var after = await registry.GetActiveConnectionIdsAsync(
            [admin.UserId],
            CancellationToken);

        Assert.Contains(connectionId, before);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.DoesNotContain(connectionId, after);
    }

    private async Task<LoginResult> LoginAsync(string username, string password)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password },
            CancellationToken);
        var body = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new LoginResult(
            body.GetProperty("access_token").GetString()!,
            body.GetProperty("user").GetProperty("id").GetInt64());
    }

    private async Task<JsonElement> CreateUserAsync(
        string token,
        string username,
        string role)
    {
        using var response = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/admin/users",
            token,
            new
            {
                username,
                display_name = "Operations Account",
                password = "Account-Password-42!",
                roles = new[] { role },
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private async Task<JsonElement> CreateAircraftAsync(string token, string tailNumber)
    {
        using var response = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/aircraft",
            token,
            new
            {
                tail_number = tailNumber,
                display_name = "Map Demonstrator",
                model = "AeroGuard AG-20",
                status = "ACTIVE",
                notes = "Used for live operations tests.",
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private async Task AssignAsync(
        string token,
        long aircraftId,
        long userId,
        string accessLevel)
    {
        using var response = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/aircraft/{aircraftId}/assignments/{userId}",
            token,
            new { access_level = accessLevel });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<long> GetRunIdAsync(string scenario)
    {
        using var response = await _client.GetAsync(
            $"/api/runs?scenario={scenario}&limit=1",
            CancellationToken);
        var runs = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return runs[0].GetProperty("id").GetInt64();
    }

    private Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        string token) =>
        SendAuthorizedAsync(method, path, token, null);

    private async Task<HttpResponseMessage> SendAuthorizedJsonAsync(
        HttpMethod method,
        string path,
        string token,
        object body) =>
        await SendAuthorizedAsync(method, path, token, JsonContent.Create(body));

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        string token,
        HttpContent? content)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request, CancellationToken);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);

    private static object FlightRequest(
        long aircraftId,
        long runId,
        string scopeType,
        string scopeCode) =>
        new
        {
            aircraft_id = aircraftId,
            run_id = runId,
            scope_type = scopeType,
            scope_code = scopeCode,
            latitude = 52.2297,
            longitude = 21.0122,
            speed_kph = 780.0,
        };

    private static object FlightUpdate(long aircraftId, long runId, long version) =>
        new
        {
            aircraft_id = aircraftId,
            run_id = runId,
            scope_type = "CONTINENT",
            scope_code = "EU",
            latitude = 48.8566,
            longitude = 2.3522,
            speed_kph = 820.0,
            expected_version = version,
        };

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string UniqueTail() => $"AG-{Guid.NewGuid():N}"[..11].ToUpperInvariant();

    private sealed record LoginResult(string Token, long UserId);
}

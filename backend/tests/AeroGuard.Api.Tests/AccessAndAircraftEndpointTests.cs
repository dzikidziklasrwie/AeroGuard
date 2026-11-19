using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AeroGuard.Api.Tests;

public sealed class AccessAndAircraftEndpointTests(AeroGuardApiFactory factory)
    : IClassFixture<AeroGuardApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProtectedRoutesRequireBearerAuthentication()
    {
        using var aircraft = await _client.GetAsync("/api/aircraft", CancellationToken);
        using var users = await _client.GetAsync("/api/admin/users", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, aircraft.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, users.StatusCode);
        Assert.Contains(
            aircraft.Headers.WwwAuthenticate,
            value => string.Equals(value.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdminCanLoginReadProfileAndLogout()
    {
        var login = await LoginAsync("admin", "Test-Admin-Password-42!");
        using var profile = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/auth/me",
            login.Token);
        var body = await ReadJsonAsync(profile);
        using var logout = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/auth/logout",
            login.Token);
        using var afterLogout = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/auth/me",
            login.Token);

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal("admin", body.GetProperty("username").GetString());
        Assert.Contains("Admin", body.GetProperty("roles").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task InvalidLoginReturnsOneGenericError()
    {
        using var unknown = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = Unique("missing"), password = "Wrong-Password-42!" },
            CancellationToken);
        using var wrongPassword = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "admin", password = "Wrong-Password-42!" },
            CancellationToken);
        var unknownBody = await ReadJsonAsync(unknown);
        var wrongBody = await ReadJsonAsync(wrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(unknownBody.GetProperty("title").GetString(), wrongBody.GetProperty("title").GetString());
        Assert.Equal(unknownBody.GetProperty("detail").GetString(), wrongBody.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task AdminCreatesUsersAndProtectsLastAdministrator()
    {
        var login = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("viewer");
        using var created = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/admin/users",
            login.Token,
            new
            {
                username,
                display_name = "Flight Viewer",
                password = "Viewer-Password-42!",
                roles = new[] { "Viewer" },
            });
        var createdBody = await ReadJsonAsync(created);
        using var duplicate = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/admin/users",
            login.Token,
            new
            {
                username,
                display_name = "Duplicate Viewer",
                password = "Viewer-Password-42!",
                roles = new[] { "Viewer" },
            });
        using var retainAdmin = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/admin/users/{login.UserId}/roles",
            login.Token,
            new { roles = new[] { "Admin", "Viewer" } });
        using var removeAdmin = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/admin/users/{login.UserId}/roles",
            login.Token,
            new { roles = new[] { "Viewer" } });
        using var deactivateSelf = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/admin/users/{login.UserId}/status",
            login.Token,
            new { is_active = false });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(username, createdBody.GetProperty("username").GetString());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retainAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, removeAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deactivateSelf.StatusCode);
    }

    [Fact]
    public async Task PasswordResetAndDeactivationRevokeSessions()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("account");
        var user = await CreateUserAsync(admin.Token, username, "Viewer");
        var original = await LoginAsync(username, "Account-Password-42!");
        using var reset = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/admin/users/{user.GetProperty("id").GetInt64()}/password",
            admin.Token,
            new { password = "Replacement-Password-84!" });
        using var oldSession = await SendAuthorizedAsync(HttpMethod.Get, "/api/auth/me", original.Token);
        var replacement = await LoginAsync(username, "Replacement-Password-84!");
        using var deactivate = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/admin/users/{user.GetProperty("id").GetInt64()}/status",
            admin.Token,
            new { is_active = false });
        using var replacementSession = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/auth/me",
            replacement.Token);
        using var loginAfterDeactivation = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password = "Replacement-Password-84!" },
            CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replacementSession.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, loginAfterDeactivation.StatusCode);
    }

    [Fact]
    public async Task CustomRoleCanBeCreatedAndDeletedButSystemRoleCannot()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var roleName = $"Dispatch {Guid.NewGuid():N}"[..20];
        using var create = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/admin/roles",
            admin.Token,
            new { name = roleName, description = "Coordinates demonstration flights." });
        var role = await ReadJsonAsync(create);
        using var list = await SendAuthorizedAsync(HttpMethod.Get, "/api/admin/roles", admin.Token);
        var roles = await ReadJsonAsync(list);
        var systemRoleId = roles.EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Admin")
            .GetProperty("id")
            .GetInt64();
        using var deleteSystem = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/admin/roles/{systemRoleId}",
            admin.Token);
        using var deleteCustom = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/admin/roles/{role.GetProperty("id").GetInt64()}",
            admin.Token);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteSystem.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteCustom.StatusCode);
    }

    [Fact]
    public async Task OperatorCreatesAndUpdatesAircraftWithVersionChecks()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("operator");
        await CreateUserAsync(admin.Token, username, "Operator");
        var operation = await LoginAsync(username, "Account-Password-42!");
        var tail = UniqueTail();
        using var create = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/aircraft",
            operation.Token,
            AircraftRequest(tail.ToLowerInvariant(), "ACTIVE"));
        var aircraft = await ReadJsonAsync(create);
        var id = aircraft.GetProperty("id").GetInt64();
        using var update = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/aircraft/{id}",
            operation.Token,
            AircraftUpdate(tail, "MAINTENANCE", aircraft.GetProperty("version").GetInt64()));
        var updated = await ReadJsonAsync(update);
        using var stale = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/aircraft/{id}",
            operation.Token,
            AircraftUpdate(tail, "GROUNDED", aircraft.GetProperty("version").GetInt64()));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(tail, aircraft.GetProperty("tail_number").GetString());
        Assert.Equal("OWNER", aircraft.GetProperty("current_user_access").GetString());
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("MAINTENANCE", updated.GetProperty("status").GetString());
        Assert.Equal(2, updated.GetProperty("version").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task ViewerSeesAssignedAircraftButCannotChangeIt()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("assigned");
        var user = await CreateUserAsync(admin.Token, username, "Viewer");
        var aircraft = await CreateAircraftAsync(admin.Token, UniqueTail());
        var aircraftId = aircraft.GetProperty("id").GetInt64();
        using var assignment = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/aircraft/{aircraftId}/assignments/{user.GetProperty("id").GetInt64()}",
            admin.Token,
            new { access_level = "VIEWER" });
        var viewer = await LoginAsync(username, "Account-Password-42!");
        using var visible = await SendAuthorizedAsync(HttpMethod.Get, "/api/aircraft", viewer.Token);
        var list = await ReadJsonAsync(visible);
        using var update = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/aircraft/{aircraftId}",
            viewer.Token,
            AircraftUpdate(
                aircraft.GetProperty("tail_number").GetString()!,
                "GROUNDED",
                aircraft.GetProperty("version").GetInt64()));
        using var create = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/aircraft",
            viewer.Token,
            AircraftRequest(UniqueTail(), "ACTIVE"));

        Assert.Equal(HttpStatusCode.OK, assignment.StatusCode);
        Assert.Single(list.EnumerateArray());
        Assert.Equal("VIEWER", list[0].GetProperty("current_user_access").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task UnassignedAircraftIsHiddenFromViewer()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var username = Unique("unassigned");
        await CreateUserAsync(admin.Token, username, "Viewer");
        var aircraft = await CreateAircraftAsync(admin.Token, UniqueTail());
        var viewer = await LoginAsync(username, "Account-Password-42!");
        using var listResponse = await SendAuthorizedAsync(HttpMethod.Get, "/api/aircraft", viewer.Token);
        var list = await ReadJsonAsync(listResponse);
        using var detail = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/aircraft/{aircraft.GetProperty("id").GetInt64()}",
            viewer.Token);

        Assert.Empty(list.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task AircraftTailNumberAndFinalOwnerAreProtected()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var tail = UniqueTail();
        var aircraft = await CreateAircraftAsync(admin.Token, tail);
        using var duplicate = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/aircraft",
            admin.Token,
            AircraftRequest(tail.ToLowerInvariant(), "ACTIVE"));
        using var removeFinalOwner = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/aircraft/{aircraft.GetProperty("id").GetInt64()}/assignments/{admin.UserId}",
            admin.Token);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, removeFinalOwner.StatusCode);
    }

    [Fact]
    public async Task AdminCanDeleteAircraftAndItsOwnerAssignment()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        var aircraft = await CreateAircraftAsync(admin.Token, UniqueTail());
        var aircraftId = aircraft.GetProperty("id").GetInt64();
        using var delete = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/aircraft/{aircraftId}",
            admin.Token);
        using var detail = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/aircraft/{aircraftId}",
            admin.Token);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task AircraftValidationRejectsInvalidInput()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        using var invalidTail = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/aircraft",
            admin.Token,
            AircraftRequest("../bad", "ACTIVE"));
        using var invalidStatus = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/aircraft",
            admin.Token,
            AircraftRequest(UniqueTail(), "FLYING"));
        using var weakPassword = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "/api/admin/users",
            admin.Token,
            new
            {
                username = Unique("weak"),
                display_name = "Weak Password",
                password = "short",
                roles = new[] { "Viewer" },
            });

        Assert.Equal(HttpStatusCode.BadRequest, invalidTail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weakPassword.StatusCode);
    }

    [Fact]
    public async Task AuditLogRecordsAuthenticatedChanges()
    {
        var admin = await LoginAsync("admin", "Test-Admin-Password-42!");
        await CreateAircraftAsync(admin.Token, UniqueTail());
        using var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            "/api/admin/audit?limit=20",
            admin.Token);
        var records = await ReadJsonAsync(response);
        var actions = records.EnumerateArray()
            .Select(record => record.GetProperty("action").GetString())
            .ToArray();

        Assert.Contains("auth.login", actions);
        Assert.Contains("aircraft.create", actions);
        Assert.All(records.EnumerateArray(), record => Assert.True(record.TryGetProperty("occurred_at_utc", out _)));
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
                display_name = "Portfolio Account",
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
            AircraftRequest(tailNumber, "ACTIVE"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
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

    private static object AircraftRequest(string tailNumber, string status) =>
        new
        {
            tail_number = tailNumber,
            display_name = "Portfolio Demonstrator",
            model = "AeroGuard AG-20",
            status,
            notes = "Used for access and fleet tests.",
        };

    private static object AircraftUpdate(string tailNumber, string status, long version) =>
        new
        {
            tail_number = tailNumber,
            display_name = "Portfolio Demonstrator",
            model = "AeroGuard AG-20",
            status,
            notes = "Updated through the versioned REST endpoint.",
            expected_version = version,
        };

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string UniqueTail() => $"AG-{Guid.NewGuid():N}"[..11].ToUpperInvariant();

    private sealed record LoginResult(string Token, long UserId);
}

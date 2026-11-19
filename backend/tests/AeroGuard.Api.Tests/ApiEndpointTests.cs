using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AeroGuard.Api.Tests;

public sealed class ApiEndpointTests(AeroGuardApiFactory factory)
    : IClassFixture<AeroGuardApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HealthReportsReadySqliteArchive()
    {
        using var response = await _client.GetAsync("/api/health", CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ready", body.GetProperty("status").GetString());
        Assert.Equal("sqlite", body.GetProperty("database").GetString());
        Assert.Equal(7, body.GetProperty("run_count").GetInt64());
    }

    [Fact]
    public async Task ScenariosExposeApiReplayUrls()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>("/api/scenarios", CancellationToken);
        var scenarios = body.GetProperty("scenarios");

        Assert.Equal(1, body.GetProperty("schema_version").GetInt32());
        Assert.Equal(7, scenarios.GetArrayLength());
        Assert.All(
            scenarios.EnumerateArray(),
            scenario => Assert.StartsWith(
                "/api/runs/",
                scenario.GetProperty("file").GetString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunsCanBeFilteredByScenarioAndLimit()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>(
            "/api/runs?scenario=normal-operation&limit=1",
            CancellationToken);

        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal("normal-operation", body[0].GetProperty("scenario").GetString());
        Assert.Equal(64, body[0].GetProperty("source_sha256").GetString()!.Length);
    }

    [Fact]
    public async Task RunsCanBeFilteredByFinalState()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>(
            "/api/runs?finalState=NORMAL",
            CancellationToken);

        Assert.Equal(7, body.GetArrayLength());
        Assert.All(
            body.EnumerateArray(),
            run => Assert.Equal("NORMAL", run.GetProperty("final_state").GetString()));
    }

    [Fact]
    public async Task RunDetailAndReplayMatchStoredMetadata()
    {
        var runs = await _client.GetFromJsonAsync<JsonElement>(
            "/api/runs?scenario=normal-operation&limit=1",
            CancellationToken);
        var run = runs[0];
        var id = run.GetProperty("id").GetInt64();

        var detail = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/runs/{id}",
            CancellationToken);
        using var replayResponse = await _client.GetAsync(
            $"/api/runs/{id}/replay",
            CancellationToken);
        var replay = await replayResponse.Content.ReadAsStringAsync(CancellationToken);

        Assert.Equal("normal-operation", detail.GetProperty("scenario").GetString());
        Assert.Equal("application/x-ndjson", replayResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"record_type\":\"metadata\"", replay, StringComparison.Ordinal);
        Assert.Contains("\"record_type\":\"summary\"", replay, StringComparison.Ordinal);
        Assert.StartsWith("\"sha256-", replayResponse.Headers.ETag?.Tag, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordsPreserveSourceOrder()
    {
        var runs = await _client.GetFromJsonAsync<JsonElement>(
            "/api/runs?scenario=communication-interruption&limit=1",
            CancellationToken);
        var id = runs[0].GetProperty("id").GetInt64();
        var records = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/runs/{id}/records?limit=5000",
            CancellationToken);

        var ordinals = records.EnumerateArray()
            .Select(record => record.GetProperty("ordinal").GetInt64())
            .ToArray();
        Assert.NotEmpty(ordinals);
        Assert.Equal(Enumerable.Range(0, ordinals.Length).Select(value => (long)value), ordinals);
    }

    [Fact]
    public async Task RecordsCanBeFilteredByTypeSubsystemAndTime()
    {
        var runs = await _client.GetFromJsonAsync<JsonElement>(
            "/api/runs?scenario=normal-operation&limit=1",
            CancellationToken);
        var id = runs[0].GetProperty("id").GetInt64();
        var records = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/runs/{id}/records?recordType=telemetry&subsystem=power&fromMs=1000&toMs=3000&limit=100",
            CancellationToken);

        Assert.NotEmpty(records.EnumerateArray());
        Assert.All(records.EnumerateArray(), record =>
        {
            Assert.Equal("telemetry", record.GetProperty("record_type").GetString());
            Assert.Equal("power", record.GetProperty("subsystem").GetString());
            Assert.InRange(record.GetProperty("timestamp_ms").GetInt64(), 1000, 3000);
        });
    }

    [Theory]
    [InlineData("/api/runs?scenario=../escape")]
    [InlineData("/api/runs?finalState=UNKNOWN_STATE")]
    [InlineData("/api/runs?limit=0")]
    [InlineData("/api/runs/1/records?fromMs=10&toMs=5")]
    [InlineData("/api/runs/1/records?limit=5001")]
    public async Task InvalidFiltersReturnBadRequest(string path)
    {
        using var response = await _client.GetAsync(path, CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingRunReturnsNotFound()
    {
        using var detail = await _client.GetAsync("/api/runs/999999", CancellationToken);
        using var replay = await _client.GetAsync("/api/runs/999999/replay", CancellationToken);
        using var records = await _client.GetAsync("/api/runs/999999/records", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, records.StatusCode);
    }

    [Fact]
    public async Task ResponsesIncludeApiSecurityHeaders()
    {
        using var response = await _client.GetAsync("/api/health", CancellationToken);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApiDocumentListsArchiveEndpoints()
    {
        using var response = await _client.GetAsync("/openapi/v1.json", CancellationToken);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var paths = document.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(paths.TryGetProperty("/api/runs", out _));
        Assert.True(paths.TryGetProperty("/api/runs/{id}/records", out _));
    }
}

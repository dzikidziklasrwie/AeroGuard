using System.Text.Json;
using System.Security.Claims;
using AeroGuard.Api.Configuration;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Data;
using AeroGuard.Api.Endpoints;
using AeroGuard.Api.Errors;
using AeroGuard.Api.Fleet;
using AeroGuard.Api.Identity;
using AeroGuard.Api.Import;
using AeroGuard.Api.Models;
using AeroGuard.Api.Operations;
using AeroGuard.Api.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var trustProxyHeaders = builder.Configuration.GetValue<bool>("AEROGUARD_TRUST_PROXY_HEADERS");

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
if (trustProxyHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddPolicy("operations-write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 90,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddPolicy("operations-chat", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 60,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
    options.AddPolicy("operations-hub", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1),
            }));
});

var archiveOptions = AeroGuardOptions.FromConfiguration(
    builder.Configuration,
    builder.Environment.ContentRootPath);
builder.Services.AddSingleton(archiveOptions);
builder.Services.AddSingleton<ISqliteConnectionFactory>(
    new SqliteConnectionFactory(archiveOptions.DatabasePath));
builder.Services.AddSingleton<ArchiveRepository>();
builder.Services.AddSingleton(provider => new DatabaseInitializer(
    provider.GetRequiredService<ISqliteConnectionFactory>(),
    archiveOptions.SchemaPath));
builder.Services.AddSingleton(provider => new ArtifactImporter(
    archiveOptions.DataDirectory,
    provider.GetRequiredService<ArchiveRepository>()));

var accessOptions = AccessOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(accessOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IIdentityRepository, IdentityRepository>();
builder.Services.AddSingleton<IAuditRepository, AuditRepository>();
builder.Services.AddSingleton<IAircraftRepository, AircraftRepository>();
builder.Services.AddSingleton<IOperationsRepository, OperationsRepository>();
builder.Services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddSingleton<IAccessTokenIssuer, SecureAccessTokenIssuer>();
builder.Services.AddSingleton<AeroGuard.Api.Identity.AuthenticationService>();
builder.Services.AddSingleton<AccountAdministrationService>();
builder.Services.AddSingleton<BootstrapAdminService>();
builder.Services.AddSingleton<AircraftService>();
builder.Services.AddSingleton<OperationPermissionService>();
builder.Services.AddSingleton<FlightTelemetryCache>();
builder.Services.AddSingleton<FlightSnapshotFactory>();
builder.Services.AddSingleton<OperationsBroadcaster>();
builder.Services.AddSingleton<OperationsConnectionRegistry>();
builder.Services.AddSingleton<OperationsBroadcastRefreshSignal>();
builder.Services.AddSingleton<OperationsService>();
builder.Services.AddHostedService<FlightSimulationWorker>();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.PayloadSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    });
builder.Services
    .AddAuthentication(BearerTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
        BearerTokenAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole(BuiltInRoles.Admin));

var app = builder.Build();

if (trustProxyHeaders)
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapOpenApi();

await app.Services
    .GetRequiredService<DatabaseInitializer>()
    .InitializeAsync(app.Lifetime.ApplicationStopping);
await app.Services
    .GetRequiredService<BootstrapAdminService>()
    .EnsureAdminAsync(app.Lifetime.ApplicationStopping);
var repository = app.Services.GetRequiredService<ArchiveRepository>();
var importReport = await app.Services
    .GetRequiredService<ArtifactImporter>()
    .ImportManifestAsync(app.Lifetime.ApplicationStopping);
app.Logger.LogInformation(
    "Archive ready: {Imported} imported, {Skipped} already present, {ManifestEntries} listed",
    importReport.Imported,
    importReport.Skipped,
    importReport.ManifestEntries);

app.MapGet("/api/health", async (ArchiveRepository archive, CancellationToken cancellationToken) =>
    TypedResults.Ok(new HealthDto(
        SchemaVersion: 1,
        Status: "ready",
        Database: "sqlite",
        RunCount: await archive.CountRunsAsync(cancellationToken))))
    .WithName("GetHealth")
    .WithSummary("Report API and archive status");

app.MapGet("/api/scenarios", async (ArchiveRepository archive, CancellationToken cancellationToken) =>
{
    var runs = await archive.GetLatestScenarioRunsAsync(cancellationToken);
    return TypedResults.Ok(new ScenarioManifestDto(
        SchemaVersion: 1,
        GeneratedBy: "AeroGuard REST API",
        Scenarios: runs.Select(run => new ScenarioDto(
            Id: run.Scenario,
            Label: run.Label,
            Description: run.Description,
            File: ReplayUrl(run.Id),
            DurationMs: run.DurationMs,
            TickMs: run.TickMs,
            FinalState: run.FinalState)).ToArray()));
})
    .WithName("GetScenarios")
    .WithSummary("List the newest archived run for each scenario");

app.MapGet("/api/runs", async Task<Results<Ok<IReadOnlyList<RunDto>>, ValidationProblem>> (
    string? scenario,
    string? finalState,
    int? limit,
    ArchiveRepository archive,
    AeroGuardOptions options,
    CancellationToken cancellationToken) =>
{
    var errors = ApiQueryValidation.ValidateRunFilters(scenario, finalState, limit, options);
    if (errors.Count > 0)
    {
        return TypedResults.ValidationProblem(errors);
    }

    var runs = await archive.GetRunsAsync(
        scenario,
        finalState,
        limit ?? options.DefaultRunLimit,
        cancellationToken);
    return TypedResults.Ok<IReadOnlyList<RunDto>>(runs.Select(ToRunDto).ToArray());
})
    .WithName("GetRuns")
    .WithSummary("Query archived simulation runs");

app.MapGet("/api/runs/{id:long:min(1)}", async Task<Results<Ok<RunDetailDto>, NotFound>> (
    long id,
    ArchiveRepository archive,
    CancellationToken cancellationToken) =>
{
    var run = await archive.GetRunAsync(id, cancellationToken);
    return run is null
        ? TypedResults.NotFound()
        : TypedResults.Ok(ToRunDetailDto(run));
})
    .WithName("GetRun")
    .WithSummary("Get one archived simulation run");

app.MapGet("/api/runs/{id:long:min(1)}/replay", ArchivedReplayEndpoint.GetAsync)
    .WithName("GetReplay")
    .WithSummary("Download the original JSONL artifact")
    .Produces(StatusCodes.Status200OK, contentType: "application/x-ndjson")
    .Produces(StatusCodes.Status304NotModified)
    .Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/runs/{id:long:min(1)}/records", async Task<IResult> (
    long id,
    string? recordType,
    string? severity,
    string? subsystem,
    long? fromMs,
    long? toMs,
    int? limit,
    ArchiveRepository archive,
    AeroGuardOptions options,
    CancellationToken cancellationToken) =>
{
    var errors = ApiQueryValidation.ValidateRecordFilters(
        recordType,
        severity,
        subsystem,
        fromMs,
        toMs,
        limit,
        options);
    if (errors.Count > 0)
    {
        return TypedResults.ValidationProblem(errors);
    }

    if (await archive.GetRunAsync(id, cancellationToken) is null)
    {
        return TypedResults.NotFound();
    }

    var records = await archive.GetRecordsAsync(
        id,
        new RecordFilter(
            recordType,
            severity,
            subsystem,
            fromMs,
            toMs,
            limit ?? options.DefaultRecordLimit),
        cancellationToken);

    var response = records.Select(record =>
    {
        using var document = JsonDocument.Parse(record.RawJson);
        return new RecordDto(
            record.Ordinal,
            record.RecordType,
            record.TimestampMs,
            record.Severity,
            record.Subsystem,
            document.RootElement.Clone());
    }).ToArray();

    return TypedResults.Ok(response);
})
    .WithName("GetRecords")
    .WithSummary("Filter ordered records from one archived run")
    .Produces<IReadOnlyList<RecordDto>>()
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status404NotFound);

app.MapAuthenticationEndpoints();
app.MapAdministrationEndpoints();
app.MapAircraftEndpoints();
app.MapOperationsEndpoints();
app.MapHub<OperationsHub>("/hubs/operations")
    .RequireRateLimiting("operations-hub");

app.Run();

static string ReplayUrl(long id) => $"/api/runs/{id}/replay";

static string RecordsUrl(long id) => $"/api/runs/{id}/records";

static RunDto ToRunDto(StoredRun run) =>
    new(
        run.Id,
        run.Scenario,
        run.Label,
        run.Description,
        run.FinalState,
        run.DurationMs,
        run.TickMs,
        run.RecordCount,
        run.SourceSha256,
        run.ImportedAtUtc,
        ReplayUrl(run.Id),
        RecordsUrl(run.Id));

static RunDetailDto ToRunDetailDto(StoredRun run) =>
    new(
        run.Id,
        run.SchemaVersion,
        run.Scenario,
        run.Label,
        run.Description,
        run.SourceFile,
        run.SourceSha256,
        run.Seed,
        run.TickMs,
        run.DurationMs,
        run.FinalState,
        run.RecordCount,
        run.ImportedAtUtc,
        ReplayUrl(run.Id),
        RecordsUrl(run.Id));

static string RateLimitKey(HttpContext context) =>
    context.User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";

public partial class Program;

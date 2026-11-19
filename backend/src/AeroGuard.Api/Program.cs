using System.Text.Json;
using AeroGuard.Api.Configuration;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Data;
using AeroGuard.Api.Import;
using AeroGuard.Api.Models;
using AeroGuard.Api.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
});
builder.Services.AddOpenApi();

var archiveOptions = AeroGuardOptions.FromConfiguration(
    builder.Configuration,
    builder.Environment.ContentRootPath);
builder.Services.AddSingleton(archiveOptions);
builder.Services.AddSingleton(new ArchiveRepository(
    archiveOptions.DatabasePath,
    archiveOptions.SchemaPath));
builder.Services.AddSingleton(provider => new ArtifactImporter(
    archiveOptions.DataDirectory,
    provider.GetRequiredService<ArchiveRepository>()));

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

app.MapOpenApi();

var repository = app.Services.GetRequiredService<ArchiveRepository>();
await repository.InitializeAsync(app.Lifetime.ApplicationStopping);
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

app.MapGet("/api/runs/{id:long:min(1)}/replay", async Task<IResult> (
    long id,
    HttpContext context,
    ArchiveRepository archive,
    CancellationToken cancellationToken) =>
{
    var run = await archive.GetRunAsync(id, cancellationToken);
    if (run is null)
    {
        return TypedResults.NotFound();
    }

    var bytes = await archive.GetReplayAsync(id, cancellationToken);
    if (bytes is null)
    {
        return TypedResults.NotFound();
    }

    context.Response.Headers.ETag = $"\"sha256-{run.SourceSha256}\"";
    return Results.File(
        bytes,
        contentType: "application/x-ndjson",
        fileDownloadName: $"{run.Scenario}.jsonl");
})
    .WithName("GetReplay")
    .WithSummary("Download the original JSONL artifact")
    .Produces(StatusCodes.Status200OK, contentType: "application/x-ndjson")
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

public partial class Program;

using System.Text.Json;

namespace AeroGuard.Api.Contracts;

public sealed record HealthDto(
    int SchemaVersion,
    string Status,
    string Database,
    long RunCount);

public sealed record ScenarioManifestDto(
    int SchemaVersion,
    string GeneratedBy,
    IReadOnlyList<ScenarioDto> Scenarios);

public sealed record ScenarioDto(
    string Id,
    string Label,
    string Description,
    string File,
    long DurationMs,
    long TickMs,
    string FinalState);

public sealed record RunDto(
    long Id,
    string Scenario,
    string Label,
    string Description,
    string FinalState,
    long DurationMs,
    long TickMs,
    long RecordCount,
    string SourceSha256,
    DateTimeOffset ImportedAtUtc,
    string ReplayUrl,
    string RecordsUrl);

public sealed record RunDetailDto(
    long Id,
    int SchemaVersion,
    string Scenario,
    string Label,
    string Description,
    string SourceFile,
    string SourceSha256,
    long Seed,
    long TickMs,
    long DurationMs,
    string FinalState,
    long RecordCount,
    DateTimeOffset ImportedAtUtc,
    string ReplayUrl,
    string RecordsUrl);

public sealed record RecordDto(
    long Ordinal,
    string RecordType,
    long TimestampMs,
    string? Severity,
    string? Subsystem,
    JsonElement Record);

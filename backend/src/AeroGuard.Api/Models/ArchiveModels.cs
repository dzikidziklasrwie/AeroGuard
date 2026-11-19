namespace AeroGuard.Api.Models;

public sealed record ValidatedRecord(
    long Ordinal,
    string RecordType,
    long TimestampMs,
    string? Severity,
    string? Subsystem,
    string RawJson);

public sealed record ValidatedRun(
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
    byte[] ReplayBytes,
    IReadOnlyList<ValidatedRecord> Records);

public sealed record StoredRun(
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
    DateTimeOffset ImportedAtUtc);

public sealed record StoredRecord(
    long Ordinal,
    string RecordType,
    long TimestampMs,
    string? Severity,
    string? Subsystem,
    string RawJson);

public sealed record RecordFilter(
    string? RecordType,
    string? Severity,
    string? Subsystem,
    long? FromMs,
    long? ToMs,
    int Limit);

public sealed record ImportReport(int Imported, int Skipped, int ManifestEntries);

public enum ImportDisposition
{
    Imported,
    AlreadyPresent,
}

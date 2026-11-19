using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AeroGuard.Api.Data;
using AeroGuard.Api.Models;

namespace AeroGuard.Api.Import;

public sealed class ArtifactImporter
{
    private static readonly HashSet<string> RecordTypes = new(StringComparer.Ordinal)
    {
        "metadata",
        "telemetry",
        "event",
        "state_transition",
        "summary",
    };

    private static readonly HashSet<string> HealthStates = new(StringComparer.Ordinal)
    {
        "UNKNOWN",
        "HEALTHY",
        "DEGRADED",
        "FAULT",
    };

    private static readonly HashSet<string> SystemStates = new(StringComparer.Ordinal)
    {
        "INITIALIZING",
        "NORMAL",
        "DEGRADED",
        "FAULT",
        "RECOVERY",
    };

    private static readonly HashSet<string> Severities = new(StringComparer.Ordinal)
    {
        "DEBUG",
        "INFO",
        "WARNING",
        "ERROR",
        "CRITICAL",
    };

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly string _dataDirectory;
    private readonly ArchiveRepository _repository;

    public ArtifactImporter(string dataDirectory, ArchiveRepository repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(repository);

        _dataDirectory = Path.GetFullPath(dataDirectory);
        _repository = repository;
    }

    public async Task<ImportReport> ImportManifestAsync(
        CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(_dataDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new ArtifactImportException($"Manifest not found: {manifestPath}");
        }

        ArtifactManifest manifest;
        try
        {
            await using var stream = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16_384,
                useAsync: true);
            manifest = await JsonSerializer.DeserializeAsync<ArtifactManifest>(
                    stream,
                    ManifestJsonOptions,
                    cancellationToken)
                ?? throw new ArtifactImportException("manifest.json must contain a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new ArtifactImportException("manifest.json is not valid schema-v1 JSON.", exception);
        }

        var entries = ValidateManifest(manifest);
        var imported = 0;
        var skipped = 0;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var artifact = await ReadAndValidateArtifactAsync(entry, cancellationToken);
            var disposition = await _repository.ImportAsync(artifact, cancellationToken);
            if (disposition == ImportDisposition.Imported)
            {
                imported++;
            }
            else
            {
                skipped++;
            }
        }

        return new ImportReport(imported, skipped, entries.Count);
    }

    private IReadOnlyList<ValidatedManifestEntry> ValidateManifest(ArtifactManifest manifest)
    {
        if (manifest.SchemaVersion != 1)
        {
            throw new ArtifactImportException("manifest.json must use schema_version 1.");
        }

        if (manifest.Scenarios is not { Count: > 0 })
        {
            throw new ArtifactImportException("manifest.json must list at least one scenario.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validated = new List<ValidatedManifestEntry>(manifest.Scenarios.Count);

        foreach (var entry in manifest.Scenarios)
        {
            var id = RequireText(entry.Id, "manifest scenario id");
            var label = RequireText(entry.Label, $"label for scenario '{id}'");
            var description = RequireText(entry.Description, $"description for scenario '{id}'");
            var file = RequireText(entry.File, $"file for scenario '{id}'");
            var finalState = RequireText(entry.FinalState, $"final_state for scenario '{id}'");

            if (!ids.Add(id))
            {
                throw new ArtifactImportException($"manifest.json repeats scenario id '{id}'.");
            }

            if (!files.Add(file))
            {
                throw new ArtifactImportException($"manifest.json repeats artifact file '{file}'.");
            }

            if (entry.DurationMs is null or < 0 || entry.TickMs is null or < 0)
            {
                throw new ArtifactImportException(
                    $"Scenario '{id}' must have non-negative duration_ms and tick_ms values.");
            }

            if (!SystemStates.Contains(finalState))
            {
                throw new ArtifactImportException(
                    $"Scenario '{id}' has unknown final_state '{finalState}'.");
            }

            var artifactPath = ResolveArtifactPath(file);
            validated.Add(new ValidatedManifestEntry(
                id,
                label,
                description,
                file,
                artifactPath,
                entry.DurationMs.Value,
                entry.TickMs.Value,
                finalState));
        }

        return validated;
    }

    private string ResolveArtifactPath(string file)
    {
        if (Path.IsPathRooted(file)
            || file.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || file.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || !string.Equals(file, Path.GetFileName(file), StringComparison.Ordinal)
            || !string.Equals(Path.GetExtension(file), ".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArtifactImportException(
                $"Artifact path '{file}' is unsafe. Manifest files must be .jsonl base names.");
        }

        var path = Path.GetFullPath(Path.Combine(_dataDirectory, file));
        var relative = Path.GetRelativePath(_dataDirectory, path);
        if (relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new ArtifactImportException($"Artifact path '{file}' leaves the data directory.");
        }

        if (!File.Exists(path))
        {
            throw new ArtifactImportException($"Artifact not found: {file}");
        }

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new ArtifactImportException($"Artifact '{file}' must not be a symbolic link.");
        }

        return path;
    }

    private static async Task<byte[]> ReadArtifactBytesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (IOException exception)
        {
            throw new ArtifactImportException(
                $"Artifact '{Path.GetFileName(path)}' could not be read.",
                exception);
        }
    }

    private static string DecodeArtifact(byte[] bytes, string file)
    {
        if (bytes.Length == 0)
        {
            throw new ArtifactImportException($"Artifact '{file}' is empty.");
        }

        try
        {
            var text = StrictUtf8.GetString(bytes);
            return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
        }
        catch (DecoderFallbackException exception)
        {
            throw new ArtifactImportException($"Artifact '{file}' is not valid UTF-8.", exception);
        }
    }

    private static IReadOnlyList<string> SplitJsonLines(string text, string file)
    {
        var segments = text.Split('\n');
        var count = segments.Length;
        if (count > 0 && segments[^1].Length == 0)
        {
            count--;
        }

        var lines = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var line = segments[index];
            if (line.EndsWith('\r'))
            {
                line = line[..^1];
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                throw new ArtifactImportException(
                    $"Artifact '{file}' has an empty JSONL record at line {index + 1}.");
            }

            lines.Add(line);
        }

        if (lines.Count < 2)
        {
            throw new ArtifactImportException(
                $"Artifact '{file}' must contain metadata and summary records.");
        }

        return lines;
    }

    private static JsonElement ParseRecord(string line, string file, int lineNumber, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(line, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        }
        catch (JsonException exception)
        {
            throw RecordError(file, lineNumber, "invalid JSON", exception);
        }

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw RecordError(file, lineNumber, "record must be a JSON object");
        }

        return root;
    }

    private static void ValidateMetadata(
        JsonElement root,
        string file,
        int lineNumber,
        out string scenario,
        out string description,
        out long seed,
        out long tickMs,
        out long durationMs)
    {
        scenario = RequiredString(root, "scenario", file, lineNumber);
        description = RequiredString(root, "description", file, lineNumber, allowEmpty: true);
        seed = RequiredInteger(root, "seed", file, lineNumber);
        tickMs = RequiredNonNegativeInteger(root, "tick_ms", file, lineNumber);
        durationMs = RequiredNonNegativeInteger(root, "duration_ms", file, lineNumber);
        var subsystems = RequiredProperty(root, "subsystems", file, lineNumber);
        if (subsystems.ValueKind != JsonValueKind.Array
            || subsystems.GetArrayLength() == 0
            || subsystems.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String))
        {
            throw RecordError(file, lineNumber, "subsystems must be a non-empty string array");
        }
    }

    private static void ValidateTelemetry(JsonElement root, string file, int lineNumber)
    {
        _ = RequiredNonNegativeInteger(root, "sequence", file, lineNumber);
        _ = RequiredString(root, "subsystem", file, lineNumber);
        var health = RequiredString(root, "health", file, lineNumber);
        if (!HealthStates.Contains(health))
        {
            throw RecordError(file, lineNumber, $"unknown health state '{health}'");
        }

        if (RequiredProperty(root, "valid", file, lineNumber).ValueKind is not JsonValueKind.True
            and not JsonValueKind.False)
        {
            throw RecordError(file, lineNumber, "valid must be a boolean");
        }

        _ = RequiredString(root, "reason", file, lineNumber, allowEmpty: true);
        if (RequiredProperty(root, "data", file, lineNumber).ValueKind != JsonValueKind.Object)
        {
            throw RecordError(file, lineNumber, "data must be an object");
        }
    }

    private static void ValidateEvent(JsonElement root, string file, int lineNumber)
    {
        _ = ReadSeverity(root, file, lineNumber);
        _ = RequiredString(root, "event_code", file, lineNumber);
        var subsystem = RequiredProperty(root, "subsystem", file, lineNumber);
        if (subsystem.ValueKind != JsonValueKind.Null
            && (subsystem.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(subsystem.GetString())))
        {
            throw RecordError(file, lineNumber, "subsystem must be a string or null");
        }

        _ = RequiredString(root, "message", file, lineNumber, allowEmpty: true);
    }

    private static void ValidateStateTransition(JsonElement root, string file, int lineNumber)
    {
        var from = RequiredString(root, "from", file, lineNumber);
        var to = RequiredString(root, "to", file, lineNumber);
        if (!SystemStates.Contains(from) || !SystemStates.Contains(to))
        {
            throw RecordError(file, lineNumber, "state transition has an unknown state");
        }

        _ = RequiredString(root, "reason", file, lineNumber, allowEmpty: true);
        _ = ReadSeverity(root, file, lineNumber);
    }

    private static void ValidateSummary(
        JsonElement root,
        string file,
        int lineNumber,
        out string scenario,
        out long seed,
        out string finalState)
    {
        scenario = RequiredString(root, "scenario", file, lineNumber);
        seed = RequiredInteger(root, "seed", file, lineNumber);
        finalState = RequiredString(root, "final_state", file, lineNumber);
        if (!SystemStates.Contains(finalState))
        {
            throw RecordError(file, lineNumber, $"unknown final_state '{finalState}'");
        }

        foreach (var name in new[]
        {
            "ticks",
            "telemetry_count",
            "invalid_count",
            "missing_heartbeat_count",
            "dropped_message_count",
            "state_transition_count",
        })
        {
            _ = RequiredNonNegativeInteger(root, name, file, lineNumber);
        }
    }

    private static string ReadSeverity(JsonElement root, string file, int lineNumber)
    {
        var severity = RequiredString(root, "severity", file, lineNumber);
        if (!Severities.Contains(severity))
        {
            throw RecordError(file, lineNumber, $"unknown severity '{severity}'");
        }

        return severity;
    }

    private static string? ReadSubsystem(
        JsonElement root,
        string recordType,
        string file,
        int lineNumber)
    {
        if (recordType == "telemetry")
        {
            return RequiredString(root, "subsystem", file, lineNumber);
        }

        if (recordType != "event")
        {
            return null;
        }

        var value = RequiredProperty(root, "subsystem", file, lineNumber);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private static JsonElement RequiredProperty(
        JsonElement root,
        string name,
        string file,
        int lineNumber)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            throw RecordError(file, lineNumber, $"missing {name}");
        }

        return value;
    }

    private static string RequiredString(
        JsonElement root,
        string name,
        string file,
        int lineNumber,
        bool allowEmpty = false)
    {
        var value = RequiredProperty(root, name, file, lineNumber);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw RecordError(file, lineNumber, $"{name} must be a string");
        }

        var text = value.GetString() ?? string.Empty;
        if (!allowEmpty && string.IsNullOrWhiteSpace(text))
        {
            throw RecordError(file, lineNumber, $"{name} must not be empty");
        }

        return text;
    }

    private static long RequiredInteger(
        JsonElement root,
        string name,
        string file,
        int lineNumber)
    {
        var value = RequiredProperty(root, name, file, lineNumber);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result))
        {
            throw RecordError(file, lineNumber, $"{name} must be an integer");
        }

        return result;
    }

    private static long RequiredNonNegativeInteger(
        JsonElement root,
        string name,
        string file,
        int lineNumber)
    {
        var value = RequiredInteger(root, name, file, lineNumber);
        if (value < 0)
        {
            throw RecordError(file, lineNumber, $"{name} must be non-negative");
        }

        return value;
    }

    private static ArtifactImportException RecordError(
        string file,
        int lineNumber,
        string detail,
        Exception? innerException = null)
    {
        var message = $"Artifact '{file}', line {lineNumber}: {detail}.";
        return innerException is null
            ? new ArtifactImportException(message)
            : new ArtifactImportException(message, innerException);
    }

    private static string RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArtifactImportException($"Missing or empty {field}.");
        }

        return value;
    }

    private async Task<ValidatedRun> ReadAndValidateArtifactAsync(
        ValidatedManifestEntry entry,
        CancellationToken cancellationToken)
    {
        var bytes = await ReadArtifactBytesAsync(entry.ArtifactPath, cancellationToken);
        var text = DecodeArtifact(bytes, entry.File);
        var lines = SplitJsonLines(text, entry.File);
        var records = new List<ValidatedRecord>(lines.Count);

        string? metadataScenario = null;
        string? metadataDescription = null;
        long metadataSeed = 0;
        long metadataTickMs = 0;
        long metadataDurationMs = 0;
        string? summaryScenario = null;
        long summarySeed = 0;
        string? summaryFinalState = null;

        for (var index = 0; index < lines.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lineNumber = index + 1;
            var root = ParseRecord(lines[index], entry.File, lineNumber, out var document);
            using (document)
            {
                var schemaVersion = RequiredInteger(root, "schema_version", entry.File, lineNumber);
                if (schemaVersion != 1)
                {
                    throw RecordError(entry.File, lineNumber, "schema_version must be 1");
                }

                var recordType = RequiredString(root, "record_type", entry.File, lineNumber);
                if (!RecordTypes.Contains(recordType))
                {
                    throw RecordError(entry.File, lineNumber, $"unknown record_type '{recordType}'");
                }

                var timestampMs = RequiredNonNegativeInteger(
                    root,
                    "timestamp_ms",
                    entry.File,
                    lineNumber);

                if (index == 0 && recordType != "metadata")
                {
                    throw RecordError(entry.File, lineNumber, "the first record must be metadata");
                }

                if (index == lines.Count - 1 && recordType != "summary")
                {
                    throw RecordError(entry.File, lineNumber, "the last record must be summary");
                }

                if (index > 0 && recordType == "metadata")
                {
                    throw RecordError(entry.File, lineNumber, "metadata may appear only first");
                }

                if (index < lines.Count - 1 && recordType == "summary")
                {
                    throw RecordError(entry.File, lineNumber, "summary may appear only last");
                }

                string? severity = null;
                switch (recordType)
                {
                    case "metadata":
                        ValidateMetadata(
                            root,
                            entry.File,
                            lineNumber,
                            out metadataScenario,
                            out metadataDescription,
                            out metadataSeed,
                            out metadataTickMs,
                            out metadataDurationMs);
                        break;
                    case "telemetry":
                        ValidateTelemetry(root, entry.File, lineNumber);
                        break;
                    case "event":
                        ValidateEvent(root, entry.File, lineNumber);
                        severity = ReadSeverity(root, entry.File, lineNumber);
                        break;
                    case "state_transition":
                        ValidateStateTransition(root, entry.File, lineNumber);
                        severity = ReadSeverity(root, entry.File, lineNumber);
                        break;
                    case "summary":
                        ValidateSummary(
                            root,
                            entry.File,
                            lineNumber,
                            out summaryScenario,
                            out summarySeed,
                            out summaryFinalState);
                        break;
                }

                records.Add(new ValidatedRecord(
                    index,
                    recordType,
                    timestampMs,
                    severity,
                    ReadSubsystem(root, recordType, entry.File, lineNumber),
                    lines[index]));
            }
        }

        if (!string.Equals(metadataScenario, entry.Id, StringComparison.Ordinal)
            || !string.Equals(summaryScenario, entry.Id, StringComparison.Ordinal))
        {
            throw new ArtifactImportException(
                $"Artifact '{entry.File}' metadata and summary must match scenario '{entry.Id}'.");
        }

        if (!string.Equals(metadataDescription, entry.Description, StringComparison.Ordinal)
            || metadataTickMs != entry.TickMs
            || metadataDurationMs != entry.DurationMs
            || !string.Equals(summaryFinalState, entry.FinalState, StringComparison.Ordinal)
            || metadataSeed != summarySeed)
        {
            throw new ArtifactImportException(
                $"Artifact '{entry.File}' does not match its manifest metadata or summary.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new ValidatedRun(
            1,
            entry.Id,
            entry.Label,
            entry.Description,
            entry.File,
            hash,
            metadataSeed,
            entry.TickMs,
            entry.DurationMs,
            entry.FinalState,
            bytes,
            records);
    }

    private sealed record ValidatedManifestEntry(
        string Id,
        string Label,
        string Description,
        string File,
        string ArtifactPath,
        long DurationMs,
        long TickMs,
        string FinalState);
}

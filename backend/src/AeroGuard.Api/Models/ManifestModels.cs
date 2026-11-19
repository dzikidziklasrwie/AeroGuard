using System.Text.Json.Serialization;

namespace AeroGuard.Api.Models;

public sealed record ArtifactManifest
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("generated_by")]
    public string? GeneratedBy { get; init; }

    [JsonPropertyName("scenarios")]
    public List<ArtifactManifestEntry>? Scenarios { get; init; }
}

public sealed record ArtifactManifestEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("file")]
    public string? File { get; init; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; init; }

    [JsonPropertyName("tick_ms")]
    public long? TickMs { get; init; }

    [JsonPropertyName("final_state")]
    public string? FinalState { get; init; }
}

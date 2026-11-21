using System.Text.Json;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Data;
using AeroGuard.Api.Models;

namespace AeroGuard.Api.Operations;

public sealed class FlightSnapshotFactory(
    ArchiveRepository archiveRepository,
    TimeProvider timeProvider)
{
    public async Task<FlightDto> CreateAsync(
        FlightRecord flight,
        IReadOnlyCollection<string> permissions,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flight);
        var prepared = await PrepareAsync(flight, cancellationToken);
        return Create(flight, prepared, permissions, isAdmin);
    }

    public async Task<FlightSnapshotData> PrepareAsync(
        FlightRecord flight,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flight);
        var position = FlightMotion.Calculate(flight, timeProvider.GetUtcNow());
        var telemetry = await GetTelemetryAsync(flight, position.ScenarioElapsedMs, cancellationToken);
        return new FlightSnapshotData(
            position,
            telemetry,
            FlightCondition.Classify(telemetry));
    }

    public static FlightDto Create(
        FlightRecord flight,
        FlightSnapshotData prepared,
        IReadOnlyCollection<string> permissions,
        bool isAdmin,
        long eventSequence = 0)
    {
        ArgumentNullException.ThrowIfNull(flight);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(permissions);
        var isOwner = string.Equals(
            flight.CurrentUserAccess,
            Fleet.AircraftAccessLevels.Owner,
            StringComparison.Ordinal);
        var canManage = isAdmin
            || (isOwner && permissions.Contains(
                OperationPermissionNames.FlightReconfigureOwn,
                StringComparer.Ordinal));
        var canRemove = isAdmin
            || (isOwner && permissions.Contains(
                OperationPermissionNames.FlightRemoveOwn,
                StringComparer.Ordinal));

        return new FlightDto(
            flight.Id,
            flight.AircraftId,
            flight.TailNumber,
            flight.DisplayName,
            flight.OwnerUserId,
            flight.OwnerUsername,
            flight.OwnerDisplayName,
            flight.CurrentUserAccess,
            flight.ScopeType,
            flight.ScopeCode,
            prepared.Position.Latitude,
            prepared.Position.Longitude,
            prepared.Position.HeadingDeg,
            prepared.Position.RoutePhase,
            prepared.Position.RouteProgress,
            flight.SpeedKph,
            flight.RunId,
            flight.Scenario,
            flight.ScenarioLabel,
            prepared.Position.ScenarioElapsedMs,
            flight.ScenarioDurationMs,
            prepared.Condition,
            prepared.Telemetry,
            flight.Version,
            flight.StartedAtUtc,
            flight.UpdatedAtUtc,
            canManage,
            canRemove,
            eventSequence);
    }

    private async Task<IReadOnlyList<SubsystemTelemetryDto>> GetTelemetryAsync(
        FlightRecord flight,
        long scenarioElapsedMs,
        CancellationToken cancellationToken)
    {
        var tick = Math.Max(1, flight.ScenarioTickMs);
        var timestamp = scenarioElapsedMs - (scenarioElapsedMs % tick);
        var records = await archiveRepository.GetRecordsAsync(
            flight.RunId,
            new RecordFilter("telemetry", null, null, timestamp, timestamp, 20),
            cancellationToken);
        var telemetry = new List<SubsystemTelemetryDto>(records.Count);
        foreach (var record in records)
        {
            using var document = JsonDocument.Parse(record.RawJson);
            var root = document.RootElement;
            telemetry.Add(new SubsystemTelemetryDto(
                record.Subsystem ?? "unknown",
                root.TryGetProperty("health", out var health) ? health.GetString() ?? "UNKNOWN" : "UNKNOWN",
                root.TryGetProperty("valid", out var valid) && valid.ValueKind == JsonValueKind.True,
                root.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "UNKNOWN" : "UNKNOWN",
                root.TryGetProperty("data", out var data)
                    ? data.Clone()
                    : JsonSerializer.SerializeToElement(new { })));
        }

        return telemetry;
    }

}

public sealed record FlightSnapshotData(
    FlightPosition Position,
    IReadOnlyList<SubsystemTelemetryDto> Telemetry,
    string Condition);

public static class FlightCondition
{
    public static string Classify(IReadOnlyList<SubsystemTelemetryDto> telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        if (telemetry.Count != 6
            || telemetry.Select(item => item.Subsystem).Distinct(StringComparer.Ordinal).Count() != 6)
        {
            return "UNKNOWN";
        }

        if (telemetry.Any(item => item.Health is "FAULT" or "FAILED"))
        {
            return "CRITICAL";
        }

        if (telemetry.Any(item => !item.Valid || item.Health == "UNKNOWN"))
        {
            return "UNKNOWN";
        }

        return telemetry.Any(item => item.Health == "DEGRADED")
            ? "DEGRADED"
            : "HEALTHY";
    }
}

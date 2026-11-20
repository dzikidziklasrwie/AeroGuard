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
        var position = FlightMotion.Calculate(flight, timeProvider.GetUtcNow());
        var telemetry = await GetTelemetryAsync(flight, position.ScenarioElapsedMs, cancellationToken);
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
            position.Latitude,
            position.Longitude,
            position.HeadingDeg,
            flight.SpeedKph,
            flight.RunId,
            flight.Scenario,
            flight.ScenarioLabel,
            position.ScenarioElapsedMs,
            flight.ScenarioDurationMs,
            FlightCondition.Classify(telemetry),
            telemetry,
            flight.Version,
            flight.StartedAtUtc,
            flight.UpdatedAtUtc,
            canManage,
            canRemove,
            0);
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

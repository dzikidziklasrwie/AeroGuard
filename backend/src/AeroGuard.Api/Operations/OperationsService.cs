using System.Globalization;
using System.Text.Json;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Data;
using AeroGuard.Api.Errors;
using AeroGuard.Api.Fleet;
using AeroGuard.Api.Identity;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Operations;

public sealed class OperationsService(
    IOperationsRepository operationsRepository,
    IAircraftRepository aircraftRepository,
    ArchiveRepository archiveRepository,
    OperationPermissionService permissionService,
    FlightSnapshotFactory snapshotFactory,
    OperationsBroadcaster broadcaster,
    IAuditRepository auditRepository,
    TimeProvider timeProvider,
    ILogger<OperationsService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly IReadOnlySet<string> ContinentCodes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "AF", "AN", "AS", "EU", "NA", "OC", "SA",
        };

    private static readonly IReadOnlySet<string> CountryCodes =
        CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(culture => new RegionInfo(culture.Name).ThreeLetterISORegionName.ToUpperInvariant())
            .Where(code => code.Length == 3 && code.All(char.IsAsciiLetter))
            .ToHashSet(StringComparer.Ordinal);

    public async Task<IReadOnlyList<FlightDto>> GetVisibleFlightsAsync(
        CurrentUser actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var isAdmin = actor.IsInRole(BuiltInRoles.Admin);
        var permissions = await permissionService.ResolveAsync(
            actor.Id,
            actor.Roles,
            cancellationToken);
        var records = await operationsRepository.GetVisibleFlightsAsync(
            actor.Id,
            isAdmin,
            cancellationToken);
        var snapshots = new List<FlightDto>(records.Count);
        foreach (var record in records)
        {
            snapshots.Add(await snapshotFactory.CreateAsync(
                record,
                permissions,
                isAdmin,
                cancellationToken));
        }

        return snapshots;
    }

    public async Task<FlightDto> PlaceFlightAsync(
        CurrentUser actor,
        PlaceFlightRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var isAdmin = actor.IsInRole(BuiltInRoles.Admin);
        var permissions = await permissionService.ResolveAsync(
            actor.Id,
            actor.Roles,
            cancellationToken);
        RequirePermission(isAdmin, permissions, OperationPermissionNames.FlightPlaceOwn);
        var aircraft = await aircraftRepository.GetVisibleAsync(
            request.AircraftId,
            actor.Id,
            isAdmin,
            cancellationToken)
            ?? throw ApiProblemException.NotFound("The aircraft does not exist or is not assigned to this account.");
        RequireOwnerOrAdmin(actor, aircraft.CurrentUserAccess, "Only an owner can place this aircraft on the map.");
        if (!string.Equals(aircraft.Status, "ACTIVE", StringComparison.Ordinal))
        {
            throw ApiProblemException.Conflict("Set the aircraft status to ACTIVE before placing it on the map.");
        }

        await RequireRunAsync(request.RunId, cancellationToken);
        var route = ValidateRoute(
            request.ScopeType,
            request.ScopeCode,
            request.Latitude,
            request.Longitude,
            request.SpeedKph);
        var now = timeProvider.GetUtcNow();
        FlightRecord? flight;
        try
        {
            flight = await operationsRepository.CreateFlightAsync(
                request.AircraftId,
                request.RunId,
                route.ScopeType,
                route.ScopeCode,
                route.Latitude,
                route.Longitude,
                route.SpeedKph,
                RouteSeed(request.AircraftId, request.RunId, now),
                actor.Id,
                isAdmin,
                now,
                cancellationToken);
            if (flight is null)
            {
                var latest = await aircraftRepository.GetVisibleAsync(
                    request.AircraftId,
                    actor.Id,
                    isAdmin,
                    cancellationToken);
                if (latest is not null
                    && !string.Equals(latest.Status, "ACTIVE", StringComparison.Ordinal))
                {
                    throw ApiProblemException.Conflict(
                        "Set the aircraft status to ACTIVE before placing it on the map.");
                }

                throw ApiProblemException.Forbidden(
                    "Only a current owner can place this aircraft on the map.");
            }
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw ApiProblemException.Conflict("This aircraft already has an active map flight.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "flight.place",
            "flight",
            flight.Id.ToString(CultureInfo.InvariantCulture),
            new
            {
                flight.AircraftId,
                flight.TailNumber,
                flight.RunId,
                flight.Scenario,
                flight.ScopeType,
                flight.ScopeCode,
            },
            now,
            CancellationToken.None);
        var snapshot = await snapshotFactory.CreateAsync(
            flight,
            permissions,
            isAdmin,
            CancellationToken.None);
        await TryBroadcastAsync(
            token => broadcaster.BroadcastFlightAsync(flight, token),
            "placement",
            flight.Id);
        return snapshot;
    }

    public async Task<FlightDto> UpdateFlightAsync(
        CurrentUser actor,
        long flightId,
        UpdateFlightRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var isAdmin = actor.IsInRole(BuiltInRoles.Admin);
        var permissions = await permissionService.ResolveAsync(
            actor.Id,
            actor.Roles,
            cancellationToken);
        RequirePermission(isAdmin, permissions, OperationPermissionNames.FlightReconfigureOwn);
        var current = await RequireVisibleFlightAsync(actor, flightId, cancellationToken);
        RequireOwnerOrAdmin(actor, current.CurrentUserAccess, "Only an owner can reconfigure this flight.");
        if (request.AircraftId != current.AircraftId)
        {
            throw ApiProblemException.Validation(
                "aircraft_id",
                "An active flight cannot be moved to another aircraft.");
        }

        if (request.ExpectedVersion < 1)
        {
            throw ApiProblemException.Validation(
                "expected_version",
                "Use the current positive version value.");
        }

        await RequireRunAsync(request.RunId, cancellationToken);
        var route = ValidateRoute(
            request.ScopeType,
            request.ScopeCode,
            request.Latitude,
            request.Longitude,
            request.SpeedKph);
        var now = timeProvider.GetUtcNow();
        var updated = await operationsRepository.UpdateFlightAsync(
            flightId,
            request.RunId,
            route.ScopeType,
            route.ScopeCode,
            route.Latitude,
            route.Longitude,
            route.SpeedKph,
            RouteSeed(request.AircraftId, request.RunId, now),
            request.ExpectedVersion,
            actor.Id,
            isAdmin,
            now,
            cancellationToken);
        if (updated is null)
        {
            if (await operationsRepository.GetFlightAsync(
                    flightId,
                    actor.Id,
                    isAdmin,
                    cancellationToken) is null)
            {
                throw ApiProblemException.NotFound("The active flight does not exist.");
            }

            throw ApiProblemException.Conflict(
                "The flight changed after it was loaded. Refresh it and submit the update again.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "flight.update",
            "flight",
            flightId.ToString(CultureInfo.InvariantCulture),
            new
            {
                before = new { current.RunId, current.ScopeType, current.ScopeCode, current.Version },
                after = new { updated.RunId, updated.ScopeType, updated.ScopeCode, updated.Version },
            },
            now,
            CancellationToken.None);
        var snapshot = await snapshotFactory.CreateAsync(
            updated,
            permissions,
            isAdmin,
            CancellationToken.None);
        await TryBroadcastAsync(
            token => broadcaster.BroadcastFlightAsync(updated, token),
            "update",
            updated.Id);
        return snapshot;
    }

    public async Task RemoveFlightAsync(
        CurrentUser actor,
        long flightId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var isAdmin = actor.IsInRole(BuiltInRoles.Admin);
        var permissions = await permissionService.ResolveAsync(
            actor.Id,
            actor.Roles,
            cancellationToken);
        RequirePermission(isAdmin, permissions, OperationPermissionNames.FlightRemoveOwn);
        var flight = await RequireVisibleFlightAsync(actor, flightId, cancellationToken);
        RequireOwnerOrAdmin(actor, flight.CurrentUserAccess, "Only an owner can remove this flight from the map.");
        var recipients = await operationsRepository.GetAssignedUserIdsAsync(
            flightId,
            cancellationToken);
        if (!await operationsRepository.DeleteFlightAsync(
                flightId,
                actor.Id,
                isAdmin,
                cancellationToken))
        {
            throw ApiProblemException.NotFound("The active flight does not exist.");
        }

        var now = timeProvider.GetUtcNow();
        await auditRepository.AppendAsync(
            actor.Id,
            "flight.remove",
            "flight",
            flightId.ToString(CultureInfo.InvariantCulture),
            new { flight.AircraftId, flight.TailNumber },
            now,
            CancellationToken.None);
        await TryBroadcastAsync(
            token => broadcaster.BroadcastRemovedAsync(flightId, recipients, token),
            "removal",
            flightId);
    }

    public async Task<IReadOnlyList<FlightMessageDto>> GetMessagesAsync(
        CurrentUser actor,
        long flightId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (limit is < 1 or > 200)
        {
            throw ApiProblemException.Validation("limit", "Use a value from 1 to 200.");
        }

        await RequireVisibleFlightAsync(actor, flightId, cancellationToken);
        return (await operationsRepository.GetMessagesAsync(flightId, limit, cancellationToken))
            .Select(ToDto)
            .ToArray();
    }

    public async Task<FlightMessageDto> SendMessageAsync(
        CurrentUser actor,
        long flightId,
        SendFlightMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var isAdmin = actor.IsInRole(BuiltInRoles.Admin);
        var permissions = await permissionService.ResolveAsync(
            actor.Id,
            actor.Roles,
            cancellationToken);
        RequirePermission(isAdmin, permissions, OperationPermissionNames.ChatSend);
        var flight = await RequireVisibleFlightAsync(actor, flightId, cancellationToken);
        var shareType = request.ShareType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!FlightShareTypes.All.Contains(shareType))
        {
            throw ApiProblemException.Validation(
                "share_type",
                "Use TEXT, LOCATION, or TELEMETRY.");
        }

        var snapshot = await snapshotFactory.CreateAsync(
            flight,
            permissions,
            isAdmin,
            cancellationToken);
        object payload = new { };
        if (shareType == FlightShareTypes.Location)
        {
            payload = new
            {
                snapshot.Latitude,
                snapshot.Longitude,
                snapshot.HeadingDeg,
                snapshot.SpeedKph,
            };
        }
        else if (shareType == FlightShareTypes.Telemetry)
        {
            RequirePermission(isAdmin, permissions, OperationPermissionNames.TelemetryShare);
            RequireOwnerOrAdmin(actor, flight.CurrentUserAccess, "Only an owner can share live telemetry.");
            payload = new
            {
                snapshot.Condition,
                snapshot.Scenario,
                snapshot.ScenarioElapsedMs,
                snapshot.Telemetry,
            };
        }

        var message = (request.Message ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(message))
        {
            message = shareType switch
            {
                FlightShareTypes.Location => "Shared the current flight position.",
                FlightShareTypes.Telemetry => "Shared the current subsystem telemetry.",
                _ => string.Empty,
            };
        }

        if (message.Length is < 1 or > 500)
        {
            throw ApiProblemException.Validation("message", "Use 1 to 500 characters.");
        }

        var now = timeProvider.GetUtcNow();
        var stored = await operationsRepository.AppendMessageAsync(
            flightId,
            actor.Id,
            message,
            shareType,
            JsonSerializer.Serialize(payload, JsonOptions),
            now,
            cancellationToken)
            ?? throw ApiProblemException.Forbidden(
                "Flight visibility or chat permission changed before the message was stored.");
        var dto = ToDto(stored);
        var recipients = await operationsRepository.GetAssignedUserIdsAsync(
            flightId,
            CancellationToken.None);
        await TryBroadcastAsync(
            token => broadcaster.BroadcastMessageAsync(dto, recipients, token),
            "message",
            flightId);
        return dto;
    }

    private async Task TryBroadcastAsync(
        Func<CancellationToken, Task> broadcast,
        string eventName,
        long flightId)
    {
        try
        {
            await broadcast(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Flight {FlightId} {EventName} was stored, but its immediate live update failed",
                flightId,
                eventName);
        }
    }

    private async Task<FlightRecord> RequireVisibleFlightAsync(
        CurrentUser actor,
        long flightId,
        CancellationToken cancellationToken) =>
        await operationsRepository.GetFlightAsync(
            flightId,
            actor.Id,
            actor.IsInRole(BuiltInRoles.Admin),
            cancellationToken)
        ?? throw ApiProblemException.NotFound(
            "The active flight does not exist or is not assigned to this account.");

    private async Task RequireRunAsync(
        long runId,
        CancellationToken cancellationToken)
    {
        if (runId < 1 || await archiveRepository.GetRunAsync(runId, cancellationToken) is null)
        {
            throw ApiProblemException.Validation("run_id", "Select an archived scenario run.");
        }
    }

    private static ValidatedRoute ValidateRoute(
        string? scopeTypeValue,
        string? scopeCodeValue,
        double latitude,
        double longitude,
        double speedKph)
    {
        var scopeType = scopeTypeValue?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!FlightScopeTypes.All.Contains(scopeType))
        {
            throw ApiProblemException.Validation(
                "scope_type",
                "Use WORLD, CONTINENT, or COUNTRY.");
        }

        var scopeCode = scopeCodeValue?.Trim().ToUpperInvariant() ?? string.Empty;
        if (scopeType == FlightScopeTypes.World)
        {
            scopeCode = FlightScopeTypes.World;
        }
        else if (scopeType == FlightScopeTypes.Continent && !ContinentCodes.Contains(scopeCode))
        {
            throw ApiProblemException.Validation(
                "scope_code",
                "Use AF, AN, AS, EU, NA, OC, or SA for a continent route.");
        }
        else if (scopeType == FlightScopeTypes.Country && !CountryCodes.Contains(scopeCode))
        {
            throw ApiProblemException.Validation(
                "scope_code",
                "Use an ISO 3166-1 alpha-3 country code.");
        }

        if (!double.IsFinite(latitude) || latitude is < -85.0 or > 85.0)
        {
            throw ApiProblemException.Validation("latitude", "Use a latitude from -85 to 85.");
        }

        if (!double.IsFinite(longitude) || longitude is < -180.0 or > 180.0)
        {
            throw ApiProblemException.Validation("longitude", "Use a longitude from -180 to 180.");
        }

        if (!double.IsFinite(speedKph) || speedKph is < 400.0 or > 1100.0)
        {
            throw ApiProblemException.Validation("speed_kph", "Use a speed from 400 to 1100 km/h.");
        }

        return new ValidatedRoute(scopeType, scopeCode, latitude, longitude, speedKph);
    }

    private static void RequireOwnerOrAdmin(
        CurrentUser actor,
        string? currentUserAccess,
        string detail)
    {
        if (!actor.IsInRole(BuiltInRoles.Admin)
            && currentUserAccess != AircraftAccessLevels.Owner)
        {
            throw ApiProblemException.Forbidden(detail);
        }
    }

    private static void RequirePermission(
        bool isAdmin,
        IReadOnlyCollection<string> permissions,
        string permission)
    {
        if (!isAdmin && !permissions.Contains(permission, StringComparer.Ordinal))
        {
            throw ApiProblemException.Forbidden(
                "This account does not have the required operation permission.");
        }
    }

    private static long RouteSeed(long aircraftId, long runId, DateTimeOffset now)
    {
        unchecked
        {
            return Math.Abs(
                ((aircraftId * 1_103_515_245L)
                 ^ (runId * 12_345L)
                 ^ now.ToUnixTimeSeconds())
                % int.MaxValue);
        }
    }

    private static FlightMessageDto ToDto(FlightMessageRecord message)
    {
        using var document = JsonDocument.Parse(message.PayloadJson);
        return new FlightMessageDto(
            message.Id,
            message.FlightId,
            message.SenderUserId,
            message.SenderUsername,
            message.SenderDisplayName,
            message.Message,
            message.ShareType,
            document.RootElement.Clone(),
            message.SentAtUtc);
    }

    private sealed record ValidatedRoute(
        string ScopeType,
        string ScopeCode,
        double Latitude,
        double Longitude,
        double SpeedKph);
}

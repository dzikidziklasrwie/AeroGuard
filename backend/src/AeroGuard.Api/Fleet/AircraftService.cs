using System.Globalization;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Errors;
using AeroGuard.Api.Identity;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Fleet;

public sealed class AircraftService(
    IAircraftRepository aircraftRepository,
    IIdentityRepository identityRepository,
    IAuditRepository auditRepository,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AircraftDto>> GetVisibleAsync(
        CurrentUser actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var records = await aircraftRepository.GetVisibleAsync(
            actor.Id,
            actor.IsInRole(BuiltInRoles.Admin),
            cancellationToken);
        return records.Select(DtoMappings.ToDto).ToArray();
    }

    public async Task<AircraftDto> GetVisibleAsync(
        CurrentUser actor,
        long aircraftId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var aircraft = await aircraftRepository.GetVisibleAsync(
            aircraftId,
            actor.Id,
            actor.IsInRole(BuiltInRoles.Admin),
            cancellationToken)
            ?? throw ApiProblemException.NotFound("The aircraft does not exist or is not assigned to this account.");
        return aircraft.ToDto();
    }

    public async Task<AircraftDto> CreateAsync(
        CurrentUser actor,
        CreateAircraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        RequireAnyRole(actor, BuiltInRoles.Admin, BuiltInRoles.Operator);
        var values = Validate(
            request.TailNumber,
            request.DisplayName,
            request.Model,
            request.Status,
            request.Notes);
        var now = timeProvider.GetUtcNow();

        AircraftRecord aircraft;
        try
        {
            aircraft = await aircraftRepository.CreateAsync(
                values.TailNumber,
                values.DisplayName,
                values.Model,
                values.Status,
                values.Notes,
                actor.Id,
                now,
                cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw ApiProblemException.Conflict("An aircraft with that tail number already exists.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "aircraft.create",
            "aircraft",
            aircraft.Id.ToString(CultureInfo.InvariantCulture),
            new { aircraft.TailNumber, aircraft.DisplayName, aircraft.Model, aircraft.Status },
            now,
            cancellationToken);
        return aircraft.ToDto();
    }

    public async Task<AircraftDto> UpdateAsync(
        CurrentUser actor,
        long aircraftId,
        UpdateAircraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var current = await aircraftRepository.GetAsync(aircraftId, actor.Id, cancellationToken)
            ?? throw ApiProblemException.NotFound("The aircraft does not exist.");
        if (!actor.IsInRole(BuiltInRoles.Admin)
            && current.CurrentUserAccess is not (AircraftAccessLevels.Owner or AircraftAccessLevels.Maintainer))
        {
            throw ApiProblemException.Forbidden("This account cannot update the aircraft.");
        }

        if (request.ExpectedVersion < 1)
        {
            throw ApiProblemException.Validation("expected_version", "Use the current positive version value.");
        }

        var values = Validate(
            request.TailNumber,
            request.DisplayName,
            request.Model,
            request.Status,
            request.Notes);
        var now = timeProvider.GetUtcNow();
        AircraftRecord? updated;
        try
        {
            updated = await aircraftRepository.UpdateAsync(
                aircraftId,
                values.TailNumber,
                values.DisplayName,
                values.Model,
                values.Status,
                values.Notes,
                request.ExpectedVersion,
                actor.Id,
                now,
                cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw ApiProblemException.Conflict("An aircraft with that tail number already exists.");
        }

        if (updated is null)
        {
            if (await aircraftRepository.GetAsync(aircraftId, actor.Id, cancellationToken) is null)
            {
                throw ApiProblemException.NotFound("The aircraft does not exist.");
            }

            throw ApiProblemException.Conflict("The aircraft changed after it was loaded. Refresh it and submit the update again.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "aircraft.update",
            "aircraft",
            aircraftId.ToString(CultureInfo.InvariantCulture),
            new
            {
                before = new { current.TailNumber, current.DisplayName, current.Model, current.Status, current.Version },
                after = new { updated.TailNumber, updated.DisplayName, updated.Model, updated.Status, updated.Version },
            },
            now,
            cancellationToken);
        return updated.ToDto();
    }

    public async Task DeleteAsync(
        CurrentUser actor,
        long aircraftId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        RequireAnyRole(actor, BuiltInRoles.Admin);
        var aircraft = await aircraftRepository.GetAsync(aircraftId, actor.Id, cancellationToken)
            ?? throw ApiProblemException.NotFound("The aircraft does not exist.");
        if (!await aircraftRepository.DeleteAsync(aircraftId, cancellationToken))
        {
            throw ApiProblemException.NotFound("The aircraft does not exist.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "aircraft.delete",
            "aircraft",
            aircraftId.ToString(CultureInfo.InvariantCulture),
            new { aircraft.TailNumber, aircraft.DisplayName },
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AircraftAssignmentDto>> GetAssignmentsAsync(
        CurrentUser actor,
        long aircraftId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        RequireAnyRole(actor, BuiltInRoles.Admin);
        await RequireAircraftAsync(aircraftId, actor.Id, cancellationToken);
        return (await aircraftRepository.GetAssignmentsAsync(aircraftId, cancellationToken))
            .Select(DtoMappings.ToDto)
            .ToArray();
    }

    public async Task<AircraftAssignmentDto> SetAssignmentAsync(
        CurrentUser actor,
        long aircraftId,
        long userId,
        SetAircraftAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        RequireAnyRole(actor, BuiltInRoles.Admin);
        await RequireAircraftAsync(aircraftId, actor.Id, cancellationToken);
        var user = await identityRepository.GetUserAsync(userId, cancellationToken)
            ?? throw ApiProblemException.NotFound("The user does not exist.");
        if (!user.IsActive)
        {
            throw ApiProblemException.Conflict("Activate the user before assigning an aircraft.");
        }

        var accessLevel = request.AccessLevel?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!AircraftAccessLevels.All.Contains(accessLevel))
        {
            throw ApiProblemException.Validation(
                "access_level",
                "Use OWNER, MAINTAINER, or VIEWER.");
        }

        var current = (await aircraftRepository.GetAssignmentsAsync(aircraftId, cancellationToken))
            .SingleOrDefault(assignment => assignment.UserId == userId);
        if (current?.AccessLevel == AircraftAccessLevels.Owner
            && accessLevel != AircraftAccessLevels.Owner
            && await aircraftRepository.CountOwnersAsync(aircraftId, cancellationToken) <= 1)
        {
            throw ApiProblemException.Conflict("Assign another owner before changing the final owner.");
        }

        var now = timeProvider.GetUtcNow();
        var assignment = await aircraftRepository.UpsertAssignmentAsync(
            aircraftId,
            userId,
            accessLevel,
            actor.Id,
            now,
            cancellationToken);
        await auditRepository.AppendAsync(
            actor.Id,
            "aircraft.assignment.set",
            "aircraft",
            aircraftId.ToString(CultureInfo.InvariantCulture),
            new { user.Id, user.Username, before = current?.AccessLevel, after = assignment.AccessLevel },
            now,
            cancellationToken);
        return assignment.ToDto();
    }

    public async Task DeleteAssignmentAsync(
        CurrentUser actor,
        long aircraftId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        RequireAnyRole(actor, BuiltInRoles.Admin);
        await RequireAircraftAsync(aircraftId, actor.Id, cancellationToken);
        var current = (await aircraftRepository.GetAssignmentsAsync(aircraftId, cancellationToken))
            .SingleOrDefault(assignment => assignment.UserId == userId)
            ?? throw ApiProblemException.NotFound("The aircraft assignment does not exist.");
        if (current.AccessLevel == AircraftAccessLevels.Owner
            && await aircraftRepository.CountOwnersAsync(aircraftId, cancellationToken) <= 1)
        {
            throw ApiProblemException.Conflict("Assign another owner before removing the final owner.");
        }

        if (!await aircraftRepository.DeleteAssignmentAsync(aircraftId, userId, cancellationToken))
        {
            throw ApiProblemException.NotFound("The aircraft assignment does not exist.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "aircraft.assignment.delete",
            "aircraft",
            aircraftId.ToString(CultureInfo.InvariantCulture),
            new { current.UserId, current.Username, current.AccessLevel },
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private async Task<AircraftRecord> RequireAircraftAsync(
        long aircraftId,
        long currentUserId,
        CancellationToken cancellationToken) =>
        await aircraftRepository.GetAsync(aircraftId, currentUserId, cancellationToken)
        ?? throw ApiProblemException.NotFound("The aircraft does not exist.");

    private static ValidatedAircraft Validate(
        string? tailNumberValue,
        string? displayNameValue,
        string? modelValue,
        string? statusValue,
        string? notesValue)
    {
        var tailNumber = tailNumberValue?.Trim().ToUpperInvariant() ?? string.Empty;
        if (tailNumber.Length is < 2 or > 16
            || !char.IsAsciiLetterOrDigit(tailNumber[0])
            || tailNumber.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '-')))
        {
            throw ApiProblemException.Validation(
                "tail_number",
                "Use 2 to 16 ASCII letters, digits, or hyphens; start with a letter or digit.");
        }

        var displayName = displayNameValue?.Trim() ?? string.Empty;
        if (displayName.Length is < 1 or > 100)
        {
            throw ApiProblemException.Validation("display_name", "Use 1 to 100 characters.");
        }

        var model = modelValue?.Trim() ?? string.Empty;
        if (model.Length is < 1 or > 100)
        {
            throw ApiProblemException.Validation("model", "Use 1 to 100 characters.");
        }

        var status = statusValue?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!AircraftStatuses.All.Contains(status))
        {
            throw ApiProblemException.Validation(
                "status",
                "Use ACTIVE, MAINTENANCE, or GROUNDED.");
        }

        var notes = notesValue?.Trim() ?? string.Empty;
        if (notes.Length > 1000)
        {
            throw ApiProblemException.Validation("notes", "Use no more than 1000 characters.");
        }

        return new ValidatedAircraft(tailNumber, displayName, model, status, notes);
    }

    private static void RequireAnyRole(CurrentUser actor, params string[] roles)
    {
        if (!roles.Any(actor.IsInRole))
        {
            throw ApiProblemException.Forbidden("This account does not have the required role.");
        }
    }

    private sealed record ValidatedAircraft(
        string TailNumber,
        string DisplayName,
        string Model,
        string Status,
        string Notes);
}

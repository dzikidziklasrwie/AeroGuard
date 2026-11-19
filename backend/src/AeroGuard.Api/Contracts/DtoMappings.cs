using AeroGuard.Api.Fleet;
using AeroGuard.Api.Identity;

namespace AeroGuard.Api.Contracts;

public static class DtoMappings
{
    public static UserDto ToDto(this UserAccount user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.IsActive,
            user.Roles,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);

    public static RoleDto ToDto(this RoleRecord role) =>
        new(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystem,
            role.UserCount,
            role.CreatedAtUtc);

    public static AircraftDto ToDto(this AircraftRecord aircraft) =>
        new(
            aircraft.Id,
            aircraft.TailNumber,
            aircraft.DisplayName,
            aircraft.Model,
            aircraft.Status,
            aircraft.Notes,
            aircraft.Version,
            aircraft.CreatedBy,
            aircraft.CreatedAtUtc,
            aircraft.UpdatedAtUtc,
            aircraft.CurrentUserAccess);

    public static AircraftAssignmentDto ToDto(this AircraftAssignmentRecord assignment) =>
        new(
            assignment.AircraftId,
            assignment.UserId,
            assignment.Username,
            assignment.DisplayName,
            assignment.AccessLevel,
            assignment.AssignedBy,
            assignment.AssignedAtUtc);
}

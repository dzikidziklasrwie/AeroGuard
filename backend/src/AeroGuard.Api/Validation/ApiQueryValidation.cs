using AeroGuard.Api.Configuration;

namespace AeroGuard.Api.Validation;

public static class ApiQueryValidation
{
    private static readonly HashSet<string> SystemStates = new(StringComparer.Ordinal)
    {
        "INITIALIZING",
        "NORMAL",
        "DEGRADED",
        "FAULT",
        "RECOVERY",
    };

    private static readonly HashSet<string> RecordTypes = new(StringComparer.Ordinal)
    {
        "metadata",
        "telemetry",
        "event",
        "state_transition",
        "summary",
    };

    private static readonly HashSet<string> Severities = new(StringComparer.Ordinal)
    {
        "DEBUG",
        "INFO",
        "WARNING",
        "ERROR",
        "CRITICAL",
    };

    public static Dictionary<string, string[]> ValidateRunFilters(
        string? scenario,
        string? finalState,
        int? limit,
        AeroGuardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (scenario is not null && !IsSlug(scenario))
        {
            errors["scenario"] = ["Use 1 to 80 lowercase letters, digits, or hyphens."];
        }

        if (finalState is not null && !SystemStates.Contains(finalState))
        {
            errors["final_state"] = ["Use INITIALIZING, NORMAL, DEGRADED, FAULT, or RECOVERY."];
        }

        if (limit is < 1 || limit > options.MaximumRunLimit)
        {
            errors["limit"] = [$"Use a value from 1 to {options.MaximumRunLimit}."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateRecordFilters(
        string? recordType,
        string? severity,
        string? subsystem,
        long? fromMs,
        long? toMs,
        int? limit,
        AeroGuardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (recordType is not null && !RecordTypes.Contains(recordType))
        {
            errors["record_type"] = ["Use metadata, telemetry, event, state_transition, or summary."];
        }

        if (severity is not null && !Severities.Contains(severity))
        {
            errors["severity"] = ["Use DEBUG, INFO, WARNING, ERROR, or CRITICAL."];
        }

        if (subsystem is not null && !IsSlug(subsystem))
        {
            errors["subsystem"] = ["Use 1 to 80 lowercase letters, digits, or hyphens."];
        }

        if (fromMs is < 0)
        {
            errors["from_ms"] = ["Use zero or a positive integer."];
        }

        if (toMs is < 0)
        {
            errors["to_ms"] = ["Use zero or a positive integer."];
        }

        if (fromMs.HasValue && toMs.HasValue && fromMs > toMs)
        {
            errors["time_range"] = ["fromMs must be less than or equal to toMs."];
        }

        if (limit is < 1 || limit > options.MaximumRecordLimit)
        {
            errors["limit"] = [$"Use a value from 1 to {options.MaximumRecordLimit}."];
        }

        return errors;
    }

    private static bool IsSlug(string value) =>
        value.Length is >= 1 and <= 80
        && value.All(character =>
            character is >= 'a' and <= 'z'
            || character is >= '0' and <= '9'
            || character == '-');
}

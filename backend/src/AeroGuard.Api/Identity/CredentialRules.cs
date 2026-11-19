using AeroGuard.Api.Errors;

namespace AeroGuard.Api.Identity;

public static class CredentialRules
{
    public static string ValidateUsername(string? value)
    {
        var username = value?.Trim() ?? string.Empty;
        if (username.Length is < 3 or > 64
            || !char.IsAsciiLetterOrDigit(username[0])
            || username.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
        {
            throw ApiProblemException.Validation(
                "username",
                "Use 3 to 64 ASCII letters, digits, dots, underscores, or hyphens; start with a letter or digit.");
        }

        return username;
    }

    public static string ValidateDisplayName(string? value)
    {
        var displayName = value?.Trim() ?? string.Empty;
        if (displayName.Length is < 1 or > 100)
        {
            throw ApiProblemException.Validation(
                "display_name",
                "Use 1 to 100 characters.");
        }

        return displayName;
    }

    public static string ValidatePassword(string? value, bool allowWeak = false)
    {
        var password = value ?? string.Empty;
        if (password.Length > 128 || (!allowWeak && password.Length < 12) || (allowWeak && password.Length < 1))
        {
            throw ApiProblemException.Validation(
                "password",
                allowWeak
                    ? "Use 1 to 128 characters in the explicit development-only mode."
                    : "Use 12 to 128 characters.");
        }

        if (!allowWeak)
        {
            var categories = 0;
            categories += password.Any(char.IsLower) ? 1 : 0;
            categories += password.Any(char.IsUpper) ? 1 : 0;
            categories += password.Any(char.IsDigit) ? 1 : 0;
            categories += password.Any(character => !char.IsLetterOrDigit(character)) ? 1 : 0;
            if (categories < 3)
            {
                throw ApiProblemException.Validation(
                    "password",
                    "Use characters from at least three groups: lowercase, uppercase, digits, and symbols.");
            }
        }

        return password;
    }

    public static string ValidateRoleName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 40
            || !char.IsAsciiLetter(name[0])
            || name.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is ' ' or '-')))
        {
            throw ApiProblemException.Validation(
                "name",
                "Use 2 to 40 ASCII letters, digits, spaces, or hyphens; start with a letter.");
        }

        return name;
    }
}

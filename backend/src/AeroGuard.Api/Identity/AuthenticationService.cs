using AeroGuard.Api.Configuration;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Errors;
using AeroGuard.Api.Fleet;
using Microsoft.AspNetCore.Identity;

namespace AeroGuard.Api.Identity;

public sealed class AuthenticationService(
    IIdentityRepository repository,
    IAccessTokenIssuer tokenIssuer,
    IPasswordHasher<UserAccount> passwordHasher,
    IAuditRepository auditRepository,
    AccessOptions options,
    TimeProvider timeProvider)
{
    private readonly UserAccount _dummyUser = new(
        0,
        "missing-user",
        "Missing user",
        string.Empty,
        false,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        []);

    private string? _dummyHash;

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var username = request.Username?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        if (username.Length is < 3 or > 64 || password.Length is < 1 or > 128)
        {
            throw InvalidCredentials();
        }

        var user = await repository.FindUserByUsernameAsync(username, cancellationToken);
        var hash = user?.PasswordHash ?? GetDummyHash();
        var subject = user ?? _dummyUser;
        var verification = passwordHasher.VerifyHashedPassword(subject, hash, password);
        if (user is null
            || !user.IsActive
            || verification == PasswordVerificationResult.Failed)
        {
            throw InvalidCredentials();
        }

        var now = timeProvider.GetUtcNow();
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var updatedHash = passwordHasher.HashPassword(user, password);
            await repository.SetPasswordHashAsync(user.Id, updatedHash, now, cancellationToken);
        }

        var token = tokenIssuer.Issue(now, options.SessionLifetime);
        await repository.CreateSessionAsync(
            token.Hash,
            user.Id,
            now,
            token.ExpiresAtUtc,
            cancellationToken);
        await auditRepository.AppendAsync(
            user.Id,
            "auth.login",
            "user",
            user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new { user.Username },
            now,
            cancellationToken);

        return new LoginResponse(
            token.Value,
            "Bearer",
            token.ExpiresAtUtc,
            user.ToDto());
    }

    public async Task LogoutAsync(
        long sessionId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await repository.RevokeSessionAsync(sessionId, now, cancellationToken);
        await auditRepository.AppendAsync(
            userId,
            "auth.logout",
            "user",
            userId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new { session_id = sessionId },
            now,
            cancellationToken);
    }

    private string GetDummyHash() =>
        _dummyHash ??= passwordHasher.HashPassword(_dummyUser, "timing-only-password");

    private static ApiProblemException InvalidCredentials() =>
        new(
            StatusCodes.Status401Unauthorized,
            "Login failed",
            "The username or password is incorrect.");
}

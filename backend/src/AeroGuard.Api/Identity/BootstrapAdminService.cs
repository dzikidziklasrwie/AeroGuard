using AeroGuard.Api.Configuration;
using AeroGuard.Api.Fleet;
using Microsoft.AspNetCore.Identity;

namespace AeroGuard.Api.Identity;

public sealed class BootstrapAdminService(
    IIdentityRepository repository,
    IPasswordHasher<UserAccount> passwordHasher,
    IAuditRepository auditRepository,
    AccessOptions options,
    IWebHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<BootstrapAdminService> logger)
{
    public async Task EnsureAdminAsync(CancellationToken cancellationToken = default)
    {
        var username = CredentialRules.ValidateUsername(options.AdminUsername);
        var existing = await repository.FindUserByUsernameAsync(username, cancellationToken);
        if (existing is not null)
        {
            if (!existing.Roles.Contains(BuiltInRoles.Admin, StringComparer.OrdinalIgnoreCase))
            {
                var roles = existing.Roles
                    .Append(BuiltInRoles.Admin)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                await repository.ReplaceRolesAsync(
                    existing.Id,
                    roles,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
                logger.LogWarning("Bootstrap account existed without Admin role; the role was restored");
            }

            if (options.SyncAdminPassword)
            {
                var synchronizedPassword = ValidateConfiguredPassword();
                var verification = passwordHasher.VerifyHashedPassword(
                    existing,
                    existing.PasswordHash,
                    synchronizedPassword);
                if (verification != PasswordVerificationResult.Success)
                {
                    var synchronizedAt = timeProvider.GetUtcNow();
                    var synchronizedPasswordHash = passwordHasher.HashPassword(
                        existing,
                        synchronizedPassword);
                    await repository.SetPasswordHashAsync(
                        existing.Id,
                        synchronizedPasswordHash,
                        synchronizedAt,
                        cancellationToken);
                    await auditRepository.AppendAsync(
                        null,
                        "user.bootstrap.password.sync",
                        "user",
                        existing.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        new { existing.Username },
                        synchronizedAt,
                        cancellationToken);
                    logger.LogInformation(
                        "Bootstrap administrator {Username} password synchronized from runtime configuration",
                        username);
                }
            }

            return;
        }

        if (string.IsNullOrEmpty(options.AdminPassword))
        {
            logger.LogWarning(
                "No administrator exists. Set AEROGUARD_ADMIN_PASSWORD and restart to create account {Username}",
                username);
            return;
        }

        var password = ValidateConfiguredPassword();
        var now = timeProvider.GetUtcNow();
        var draft = new UserAccount(
            0,
            username,
            "AeroGuard Administrator",
            string.Empty,
            true,
            now,
            now,
            [BuiltInRoles.Admin]);
        var passwordHash = passwordHasher.HashPassword(draft, password);
        var created = await repository.CreateUserAsync(
            username,
            draft.DisplayName,
            passwordHash,
            draft.Roles,
            now,
            cancellationToken);
        await auditRepository.AppendAsync(
            null,
            "user.bootstrap",
            "user",
            created.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new { created.Username, role = BuiltInRoles.Admin },
            now,
            cancellationToken);
        logger.LogInformation("Bootstrap administrator {Username} created", username);
    }

    private string ValidateConfiguredPassword()
    {
        if (string.IsNullOrEmpty(options.AdminPassword))
        {
            throw new InvalidOperationException(
                "AEROGUARD_ADMIN_PASSWORD is required when administrator password synchronization is enabled.");
        }

        var allowWeak = options.AllowWeakDevelopmentPassword
            && (environment.IsDevelopment() || environment.IsEnvironment("Testing"));
        if (options.AllowWeakDevelopmentPassword && !allowWeak)
        {
            throw new InvalidOperationException(
                "Weak bootstrap passwords are allowed only in Development or Testing.");
        }

        return CredentialRules.ValidatePassword(options.AdminPassword, allowWeak);
    }
}

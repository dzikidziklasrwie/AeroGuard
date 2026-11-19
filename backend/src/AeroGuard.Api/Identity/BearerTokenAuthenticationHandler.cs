using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AeroGuard.Api.Identity;

public sealed class BearerTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IIdentityRepository repository,
    IAccessTokenIssuer tokenIssuer,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AeroGuardBearer";
    public const string SessionIdClaim = "aeroguard:session_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return AuthenticateResult.NoResult();
        }

        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Authorization must use the Bearer scheme.");
        }

        var token = authorization[prefix.Length..].Trim();
        if (token.Length is < 32 or > 200
            || token.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            return AuthenticateResult.Fail("The bearer token has an invalid format.");
        }

        var session = await repository.FindSessionAsync(
            tokenIssuer.Hash(token),
            timeProvider.GetUtcNow(),
            Context.RequestAborted);
        if (session is null)
        {
            return AuthenticateResult.Fail("The bearer token is invalid or expired.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.User.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, session.User.Username),
            new("display_name", session.User.DisplayName),
            new(SessionIdClaim, session.Id.ToString(CultureInfo.InvariantCulture)),
        };
        claims.AddRange(session.User.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return base.HandleChallengeAsync(properties);
    }
}

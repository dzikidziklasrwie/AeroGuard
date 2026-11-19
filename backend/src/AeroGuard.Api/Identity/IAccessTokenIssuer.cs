namespace AeroGuard.Api.Identity;

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(DateTimeOffset issuedAt, TimeSpan lifetime);

    string Hash(string token);
}

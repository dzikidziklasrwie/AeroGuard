using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace AeroGuard.Api.Identity;

public sealed class SecureAccessTokenIssuer : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(DateTimeOffset issuedAt, TimeSpan lifetime)
    {
        var value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new IssuedAccessToken(value, Hash(value), issuedAt.Add(lifetime));
    }

    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();
    }
}

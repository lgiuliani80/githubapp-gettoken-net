using GithubApp_GetToken.Config;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GithubApp_GetToken;

public class GithubUtils(ILogger<GithubUtils> logger, IOptions<Github> gh)
{
    Github GHOptions => gh.Value;

    public string GetJWTToken()
    {
        logger.LogDebug("Getting token");
                                                                
        // JWT payload
        var now = DateTimeOffset.UtcNow;
        var payload = new[]
        {
            new Claim("iat", now.ToUnixTimeSeconds().ToString()),
            new Claim("exp", now.AddMinutes(10).ToUnixTimeSeconds().ToString()),
            new Claim("iss", GHOptions.ClientId)
        };

        // Create signing credentials
        var securityKey = new RsaSecurityKey(GHOptions.RSAPrivateKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        // Create JWT token
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(payload),
            Expires = now.AddMinutes(10).UtcDateTime,
            Issuer = GHOptions.ClientId,
            SigningCredentials = credentials
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwt = tokenHandler.WriteToken(token);

        return jwt;
    }
}

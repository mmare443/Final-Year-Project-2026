using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LCC_CMS_Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LCC_CMS_Api.Services;

public sealed class JwtTokenService
{
    public const string UserIdClaim = "userId";

    private readonly JwtSettings _settings;
    private readonly byte[] _keyBytes;

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
        _keyBytes = Encoding.UTF8.GetBytes(_settings.Key);
    }

    public string CreateToken(User user, DateTime utcExpires)
    {
        var policyRole = RoleNames.ToPolicyRole(user.Role);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(UserIdClaim, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, policyRole),
            new("role", user.Role),
        };

        foreach (var alias in RoleNames.AllAliases(user.Role))
        {
            if (!claims.Any(c => c.Type == ClaimTypes.Role && c.Value == alias))
            {
                claims.Add(new Claim(ClaimTypes.Role, alias));
            }
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_keyBytes),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: utcExpires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static bool IsConfigured(IConfiguration configuration)
    {
        var key = configuration[$"{JwtSettings.SectionName}:Key"];
        return !string.IsNullOrWhiteSpace(key) && key.Trim().Length >= 32;
    }
}

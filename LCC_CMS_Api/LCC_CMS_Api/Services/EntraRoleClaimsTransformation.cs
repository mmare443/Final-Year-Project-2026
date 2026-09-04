using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace LCC_CMS_Api.Services;

/// <summary>
/// Copies Entra <c>roles</c> onto <see cref="ClaimTypes.Role"/> and adds
/// policy + SQL aliases so RequireRole("RegistrarAdmin") works whether the
/// token sent RegistrarAdmin or Registrar/Admin.
/// </summary>
public sealed class EntraRoleClaimsTransformation : IClaimsTransformation
{
    private readonly ILogger<EntraRoleClaimsTransformation> _logger;

    public EntraRoleClaimsTransformation(ILogger<EntraRoleClaimsTransformation> logger)
    {
        _logger = logger;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return Task.FromResult(principal);
        }

        var incoming = identity.Claims
            .Where(IsRoleClaim)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var added = new List<string>();
        foreach (var value in incoming)
        {
            foreach (var alias in RoleNames.AllAliases(value))
            {
                if (!identity.HasClaim(ClaimTypes.Role, alias))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, alias));
                    added.Add(alias);
                }
            }
        }

        _logger.LogInformation(
            "Role mapping. Incoming={Incoming} AddedAliases={Added}",
            incoming.Count == 0 ? "(none)" : string.Join(",", incoming),
            added.Count == 0 ? "(none)" : string.Join(",", added));

        return Task.FromResult(principal);
    }

    private static bool IsRoleClaim(Claim claim)
    {
        return claim.Type == ClaimTypes.Role
            || claim.Type == "roles"
            || claim.Type == "role"
            || claim.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
    }
}

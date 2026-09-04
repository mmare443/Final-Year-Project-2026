namespace LCC_CMS_Api.Services;

/// <summary>
/// Maps Entra / SPA policy role values to SQL CHECK strings and back.
/// Policies use RegistrarAdmin and ManagementPrincipal; users.role stores
/// Registrar/Admin and Management/Principal.
/// </summary>
public static class RoleNames
{
    public const string Student = "Student";
    public const string Lecturer = "Lecturer";
    public const string HoD = "HoD";
    public const string RegistrarAdmin = "RegistrarAdmin";
    public const string RegistrarAdminSql = "Registrar/Admin";
    public const string ManagementPrincipal = "ManagementPrincipal";
    public const string ManagementPrincipalSql = "Management/Principal";

    public static string ToPolicyRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return "";
        if (role.Equals(RegistrarAdminSql, StringComparison.OrdinalIgnoreCase)
            || role.Equals(RegistrarAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return RegistrarAdmin;
        }

        if (role.Equals(ManagementPrincipalSql, StringComparison.OrdinalIgnoreCase)
            || role.Equals(ManagementPrincipal, StringComparison.OrdinalIgnoreCase))
        {
            return ManagementPrincipal;
        }

        return role;
    }

    public static string ToSqlRole(string? role)
    {
        var policy = ToPolicyRole(role);
        return policy switch
        {
            RegistrarAdmin => RegistrarAdminSql,
            ManagementPrincipal => ManagementPrincipalSql,
            _ => policy,
        };
    }

    public static IEnumerable<string> AllAliases(string? role)
    {
        var policy = ToPolicyRole(role);
        if (string.IsNullOrEmpty(policy)) yield break;

        yield return policy;
        var sql = ToSqlRole(policy);
        if (!sql.Equals(policy, StringComparison.OrdinalIgnoreCase))
        {
            yield return sql;
        }
    }
}

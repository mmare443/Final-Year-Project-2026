using System.Security.Claims;
using LCC_CMS_Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

namespace LCC_CMS_Api.Services;

/// <summary>
/// Resolves <see cref="ICurrentUser"/> from a validated Entra JWT
/// (<c>oid</c> → <c>users.entra_id</c>) when a bearer token is present.
/// While AuthEnabled=false, unauthenticated requests still use lab
/// <c>X-User-Id</c> (<c>users.user_id</c>). Hub connections may send that
/// header as a query string. No first-row fallback.
/// </summary>
public sealed class CurrentUserService : ICurrentUser
{
    public const string LabUserIdHeader = "X-User-Id";

    private readonly LccCmsDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CurrentUserService> _logger;

    private bool _resolved;
    private CurrentUserSnapshot? _snapshot;

    public CurrentUserService(
        LccCmsDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<CurrentUserService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public int? UserId => _snapshot?.UserId;
    public string? Role => _snapshot?.Role;
    public string? Email => _snapshot?.Email;
    public int? StudentId => _snapshot?.StudentId;
    public string? StudentNumber => _snapshot?.StudentNumber;
    public int? StaffId => _snapshot?.StaffId;
    public string? JobTitle => _snapshot?.JobTitle;

    public async Task<bool> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved)
        {
            return _snapshot is not null;
        }

        _resolved = true;
        var http = _httpContextAccessor.HttpContext;
        var authEnabled = _configuration.GetValue("AuthEnabled", false);
        var entraTokenPresent = HasBearerToken(http);

        var oid = ReadObjectId(http?.User);
        if (!string.IsNullOrWhiteSpace(oid))
        {
            _logger.LogInformation("Oid resolution. Oid={Oid}", oid);
            _snapshot = await LoadByEntraIdAsync(oid, cancellationToken);
            if (_snapshot is null)
            {
                _logger.LogInformation(
                    "CurrentUser miss. Source=entra Oid={Oid} (no active users.entra_id match)",
                    oid);
            }
        }
        else
        {
            _logger.LogInformation(
                "Oid resolution. Authenticated={Authenticated} Oid=(none)",
                http?.User.Identity?.IsAuthenticated == true);
        }

        if (_snapshot is null
            && !authEnabled
            && !entraTokenPresent
            && TryReadLabUserId(http, out var labUserId))
        {
            _snapshot = await LoadByUserIdAsync(labUserId, cancellationToken);
            if (_snapshot is null)
            {
                _logger.LogInformation(
                    "CurrentUser miss. Source=lab UserId={UserId}",
                    labUserId);
            }
        }

        if (_snapshot is not null)
        {
            _logger.LogInformation(
                "CurrentUser resolved. Source={Source} UserId={UserId} Email={Email} Role={Role} StudentId={StudentId} StaffId={StaffId}",
                string.IsNullOrWhiteSpace(oid) ? "lab" : "entra",
                _snapshot.UserId,
                _snapshot.Email,
                _snapshot.Role,
                _snapshot.StudentId,
                _snapshot.StaffId);
        }
        else if (oid is null)
        {
            _logger.LogInformation("CurrentUser unresolved. No Entra oid and no lab X-User-Id.");
        }

        return _snapshot is not null;
    }

    private static bool HasBearerToken(HttpContext? http)
    {
        if (http is null) return false;
        if (http.User.Identity?.IsAuthenticated == true) return true;

        var authorization = http.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && authorization.Length > "Bearer ".Length)
        {
            return true;
        }

        return http.Request.Path.StartsWithSegments("/hubs/messages")
            && !string.IsNullOrWhiteSpace(http.Request.Query["access_token"].ToString());
    }

    private static string? ReadObjectId(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return principal.FindFirstValue(ClaimConstants.ObjectId)
            ?? principal.FindFirstValue("oid")
            ?? principal.FindFirstValue("sub");
    }

    private static bool TryReadLabUserId(HttpContext? http, out int userId)
    {
        userId = 0;
        if (http is null) return false;

        if (http.Request.Headers.TryGetValue(LabUserIdHeader, out var headerValues)
            && TryParsePositiveInt(headerValues.ToString(), out userId))
        {
            return true;
        }

        // Browsers often cannot send custom headers on the WebSocket transport.
        if (http.Request.Path.StartsWithSegments("/hubs/messages")
            && http.Request.Query.TryGetValue(LabUserIdHeader, out var queryValues)
            && TryParsePositiveInt(queryValues.ToString(), out userId))
        {
            return true;
        }

        return false;
    }

    private static bool TryParsePositiveInt(string? raw, out int userId)
    {
        return int.TryParse(raw, out userId) && userId > 0;
    }

    private Task<CurrentUserSnapshot?> LoadByEntraIdAsync(string entraId, CancellationToken cancellationToken)
    {
        var key = entraId.Trim();
        return QueryActiveUsers()
            .Where(u => u.EntraId == key)
            .Select(u => new CurrentUserSnapshot(
                u.UserId,
                u.Role,
                u.Email,
                u.Student != null ? u.Student.StudentId : (int?)null,
                u.Student != null ? u.Student.StudentNumber : null,
                u.Staff != null ? u.Staff.StaffId : (int?)null,
                u.Staff != null ? u.Staff.JobTitle : null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<CurrentUserSnapshot?> LoadByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        return QueryActiveUsers()
            .Where(u => u.UserId == userId)
            .Select(u => new CurrentUserSnapshot(
                u.UserId,
                u.Role,
                u.Email,
                u.Student != null ? u.Student.StudentId : (int?)null,
                u.Student != null ? u.Student.StudentNumber : null,
                u.Staff != null ? u.Staff.StaffId : (int?)null,
                u.Staff != null ? u.Staff.JobTitle : null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<User> QueryActiveUsers()
    {
        return _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Status == "Active");
    }

    private sealed record CurrentUserSnapshot(
        int UserId,
        string Role,
        string Email,
        int? StudentId,
        string? StudentNumber,
        int? StaffId,
        string? JobTitle);
}

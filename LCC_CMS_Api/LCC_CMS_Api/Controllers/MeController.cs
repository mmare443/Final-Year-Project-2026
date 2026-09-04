using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// Signed-in identity for the SPA. Prefers a validated Entra bearer token
/// (oid → users.entra_id). While AuthEnabled=false, lab header X-User-Id
/// remains a fallback. Profile and published results use
/// /api/students/me and /api/results/me.
/// </summary>
[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<MeController> _logger;

    public MeController(ICurrentUser currentUser, ILogger<MeController> logger)
    {
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<MeRecord>> Get(CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            _logger.LogInformation("GET /api/me unauthorized. CurrentUser did not resolve.");
            return Unauthorized();
        }

        var roleSql = _currentUser.Role ?? "";
        var role = RoleNames.ToPolicyRole(roleSql);
        _logger.LogInformation(
            "GET /api/me. UserId={UserId} Email={Email} Role={Role} RoleSql={RoleSql} StudentId={StudentId} StaffId={StaffId}",
            _currentUser.UserId,
            _currentUser.Email,
            role,
            roleSql,
            _currentUser.StudentId,
            _currentUser.StaffId);

        return Ok(new MeRecord
        {
            UserId = _currentUser.UserId.Value,
            Email = _currentUser.Email ?? "",
            Role = role,
            RoleSql = roleSql,
            StudentId = _currentUser.StudentId,
            StudentNumber = _currentUser.StudentNumber,
            StaffId = _currentUser.StaffId,
            JobTitle = _currentUser.JobTitle,
        });
    }
}

public class MeRecord
{
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string RoleSql { get; set; } = "";
    public int? StudentId { get; set; }
    public string? StudentNumber { get; set; }
    public int? StaffId { get; set; }
    public string? JobTitle { get; set; }
}

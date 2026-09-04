using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// Lightweight user directory for M8 compose (userId + email + role).
/// Not a full profile API. Auth scoping is a later phase.
/// </summary>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;

    public UsersController(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("simple")]
    public async Task<ActionResult<IEnumerable<SimpleUserRecord>>> GetSimple()
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Status == "Active")
            .OrderBy(u => u.Role)
            .ThenBy(u => u.Email)
            .Select(u => new SimpleUserRecord
            {
                UserId = u.UserId,
                Email = u.Email,
                Role = u.Role,
            })
            .ToListAsync();

        return Ok(users);
    }
}

public class SimpleUserRecord
{
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M8 Phase 1 — institutional notices. Messages and SignalR are later phases.
///
/// target_role NULL = all roles. Stored values match the schema CHECK:
/// Student, Lecturer, HoD, Registrar/Admin, Management/Principal.
/// SPA aliases RegistrarAdmin and ManagementPrincipal are accepted on write
/// and query, then stored/compared as the schema strings.
/// </summary>
[ApiController]
[Route("api/notices")]
public class NoticesController : ControllerBase
{
    private static readonly Dictionary<string, string> RoleByAlias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Student"] = "Student",
        ["Lecturer"] = "Lecturer",
        ["HoD"] = "HoD",
        ["Registrar/Admin"] = "Registrar/Admin",
        ["RegistrarAdmin"] = "Registrar/Admin",
        ["Management/Principal"] = "Management/Principal",
        ["ManagementPrincipal"] = "Management/Principal",
    };

    private readonly LccCmsDbContext _dbContext;

    public NoticesController(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoticeRecord>>> GetNotices([FromQuery] string? targetRole)
    {
        if (!TryNormalizeTargetRole(targetRole, required: false, out var role, out var error))
        {
            return BadRequest(error);
        }

        var query = NoticeGraph().AsNoTracking();
        if (role is not null)
        {
            query = query.Where(n => n.TargetRole == null || n.TargetRole == role);
        }

        var notices = await query
            .OrderByDescending(n => n.PostedAt)
            .ThenByDescending(n => n.NoticeId)
            .ToListAsync();

        return Ok(notices.Select(ToRecord));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NoticeRecord>> GetNotice(int id)
    {
        var notice = await NoticeGraph()
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NoticeId == id);
        if (notice is null) return NotFound();
        return Ok(ToRecord(notice));
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPost]
    public async Task<ActionResult<NoticeRecord>> CreateNotice([FromBody] NoticeWriteRequest request)
    {
        var error = ValidateWrite(request, out var targetRole);
        if (error is not null) return BadRequest(error);

        var author = await LoadAuthorAsync(request.AuthorId);
        if (author is null) return BadRequest("Author was not found.");

        var notice = new Notice
        {
            AuthorId = author.StaffId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            TargetRole = targetRole,
            PostedAt = DateTime.UtcNow,
        };
        _dbContext.Notices.Add(notice);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        notice.Author = author;
        return Ok(ToRecord(notice));
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPut("{id}")]
    public async Task<ActionResult<NoticeRecord>> UpdateNotice(int id, [FromBody] NoticeWriteRequest request)
    {
        var error = ValidateWrite(request, out var targetRole);
        if (error is not null) return BadRequest(error);

        var notice = await NoticeGraph().FirstOrDefaultAsync(n => n.NoticeId == id);
        if (notice is null) return NotFound();

        var author = await LoadAuthorAsync(request.AuthorId);
        if (author is null) return BadRequest("Author was not found.");

        notice.AuthorId = author.StaffId;
        notice.Title = request.Title.Trim();
        notice.Content = request.Content.Trim();
        notice.TargetRole = targetRole;

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        notice.Author = author;
        return Ok(ToRecord(notice));
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotice(int id)
    {
        var notice = await NoticeGraph().FirstOrDefaultAsync(n => n.NoticeId == id);
        if (notice is null) return NotFound();

        var record = ToRecord(notice);
        _dbContext.Notices.Remove(notice);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(record);
    }

    private string? ValidateWrite(NoticeWriteRequest request, out string? targetRole)
    {
        targetRole = null;
        if (request.AuthorId <= 0) return "Author is required.";
        if (string.IsNullOrWhiteSpace(request.Title)) return "Title is required.";
        if (request.Title.Trim().Length > 150) return "Title must be 150 characters or fewer.";
        if (string.IsNullOrWhiteSpace(request.Content)) return "Content is required.";
        if (!TryNormalizeTargetRole(request.TargetRole, required: false, out targetRole, out var roleError))
        {
            return roleError;
        }
        return null;
    }

    private static bool TryNormalizeTargetRole(
        string? value,
        bool required,
        out string? normalized,
        out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                error = "Target role is required.";
                return false;
            }
            return true;
        }

        var key = value.Trim();
        if (RoleByAlias.TryGetValue(key, out var stored))
        {
            normalized = stored;
            return true;
        }

        error = "Target role must be Student, Lecturer, HoD, Registrar/Admin, or Management/Principal, or omitted for all roles.";
        return false;
    }

    private IQueryable<Notice> NoticeGraph()
    {
        return _dbContext.Notices.Include(n => n.Author);
    }

    private async Task<Staff?> LoadAuthorAsync(int authorId)
    {
        return await _dbContext.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StaffId == authorId);
    }

    private static NoticeRecord ToRecord(Notice notice)
    {
        return new NoticeRecord
        {
            Id = notice.NoticeId,
            AuthorId = notice.AuthorId,
            AuthorJobTitle = notice.Author?.JobTitle ?? "",
            Title = notice.Title,
            Content = notice.Content,
            TargetRole = notice.TargetRole,
            PostedAt = notice.PostedAt,
        };
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the notice.";

        if (ex.InnerException is not SqlException sql)
        {
            return false;
        }

        if (sql.Number == 547)
        {
            message = "A related record was not found (author).";
            return true;
        }

        return false;
    }
}

public class NoticeRecord
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public string AuthorJobTitle { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? TargetRole { get; set; }
    public DateTime PostedAt { get; set; }
}

public class NoticeWriteRequest
{
    public int AuthorId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? TargetRole { get; set; }
}

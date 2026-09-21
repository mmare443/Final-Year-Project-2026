using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// Institutional announcements (notices table). Principal manages create,
/// edit, archive, and delete. Public and portal feeds are date-windowed
/// and exclude archived rows.
/// </summary>
[ApiController]
[Route("api/notices")]
public class NoticesController : ControllerBase
{
    private static readonly HashSet<string> Audiences = new(StringComparer.OrdinalIgnoreCase)
    {
        "Public", "Staff", "Students", "Everyone",
    };

    private static readonly HashSet<string> Priorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Normal", "High", "Urgent",
    };

    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public NoticesController(LccCmsDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [AllowAnonymous]
    [HttpGet("public")]
    public async Task<ActionResult<IEnumerable<NoticeRecord>>> GetPublic(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var notices = await ActiveFeedQuery(today)
            .Where(n => n.Audience == "Public" || n.Audience == "Everyone")
            .ToListAsync(cancellationToken);
        return Ok(OrderFeed(notices).Select(ToRecord));
    }

    [Authorize]
    [HttpGet("portal")]
    public async Task<ActionResult<IEnumerable<NoticeRecord>>> GetPortal(CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            return Unauthorized();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var student = RoleNames.ToPolicyRole(_currentUser.Role) == RoleNames.Student;
        var query = ActiveFeedQuery(today);
        query = student
            ? query.Where(n => n.Audience == "Students" || n.Audience == "Everyone")
            : query.Where(n => n.Audience == "Staff" || n.Audience == "Everyone");

        var notices = await query.ToListAsync(cancellationToken);
        return Ok(OrderFeed(notices).Select(ToRecord));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoticeRecord>>> GetAll(
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
    {
        var query = NoticeGraph().AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(n => !n.IsArchived);
        }

        var notices = await query.ToListAsync(cancellationToken);
        return Ok(OrderFeed(notices).Select(ToRecord));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<NoticeRecord>> GetNotice(int id, CancellationToken cancellationToken)
    {
        var notice = await NoticeGraph()
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NoticeId == id, cancellationToken);
        if (notice is null) return NotFound();
        return Ok(ToRecord(notice));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPost]
    public async Task<ActionResult<NoticeRecord>> CreateNotice(
        [FromBody] NoticeWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateWrite(request, out var audience, out var priority, out var start, out var end);
        if (error is not null) return BadRequest(error);

        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.StaffId is null)
        {
            return BadRequest("Sign in as Principal/staff to post an announcement.");
        }

        var author = await LoadAuthorAsync(_currentUser.StaffId.Value, cancellationToken);
        if (author is null) return BadRequest("Author was not found.");

        var notice = new Notice
        {
            AuthorId = author.StaffId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Audience = audience,
            TargetRole = ToLegacyTargetRole(audience),
            StartDate = start,
            EndDate = end,
            Priority = priority,
            IsArchived = false,
            PostedAt = DateTime.UtcNow,
        };
        _dbContext.Notices.Add(notice);

        var saved = await SaveAsync(cancellationToken);
        if (saved is not null) return saved;

        notice.Author = author;
        return Ok(ToRecord(notice));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<NoticeRecord>> UpdateNotice(
        int id,
        [FromBody] NoticeWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateWrite(request, out var audience, out var priority, out var start, out var end);
        if (error is not null) return BadRequest(error);

        var notice = await NoticeGraph().FirstOrDefaultAsync(n => n.NoticeId == id, cancellationToken);
        if (notice is null) return NotFound();

        notice.Title = request.Title.Trim();
        notice.Content = request.Content.Trim();
        notice.Audience = audience;
        notice.TargetRole = ToLegacyTargetRole(audience);
        notice.StartDate = start;
        notice.EndDate = end;
        notice.Priority = priority;

        var saved = await SaveAsync(cancellationToken);
        if (saved is not null) return saved;

        return Ok(ToRecord(notice));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPut("{id:int}/archive")]
    public async Task<ActionResult<NoticeRecord>> ArchiveNotice(int id, CancellationToken cancellationToken)
    {
        return await SetArchivedAsync(id, archived: true, cancellationToken);
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPut("{id:int}/restore")]
    public async Task<ActionResult<NoticeRecord>> RestoreNotice(int id, CancellationToken cancellationToken)
    {
        return await SetArchivedAsync(id, archived: false, cancellationToken);
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNotice(int id, CancellationToken cancellationToken)
    {
        var notice = await NoticeGraph().FirstOrDefaultAsync(n => n.NoticeId == id, cancellationToken);
        if (notice is null) return NotFound();

        var record = ToRecord(notice);
        _dbContext.Notices.Remove(notice);

        var saved = await SaveAsync(cancellationToken);
        if (saved is not null) return saved;

        return Ok(record);
    }

    private async Task<ActionResult<NoticeRecord>> SetArchivedAsync(
        int id, bool archived, CancellationToken cancellationToken)
    {
        var notice = await NoticeGraph().FirstOrDefaultAsync(n => n.NoticeId == id, cancellationToken);
        if (notice is null) return NotFound();

        notice.IsArchived = archived;
        notice.ArchivedAt = archived ? DateTime.UtcNow : null;

        var saved = await SaveAsync(cancellationToken);
        if (saved is not null) return saved;

        return Ok(ToRecord(notice));
    }

    private string? ValidateWrite(
        NoticeWriteRequest request,
        out string audience,
        out string priority,
        out DateOnly? start,
        out DateOnly? end)
    {
        audience = "Everyone";
        priority = "Normal";
        start = request.StartDate;
        end = request.EndDate;

        if (string.IsNullOrWhiteSpace(request.Title)) return "Title is required.";
        if (request.Title.Trim().Length > 150) return "Title must be 150 characters or fewer.";
        if (string.IsNullOrWhiteSpace(request.Content)) return "Content is required.";

        var audienceKey = string.IsNullOrWhiteSpace(request.Audience) ? "Everyone" : request.Audience.Trim();
        var matchAudience = Audiences.FirstOrDefault(a => a.Equals(audienceKey, StringComparison.OrdinalIgnoreCase));
        if (matchAudience is null)
        {
            return "Audience must be Public, Staff, Students, or Everyone.";
        }
        audience = matchAudience;

        var priorityKey = string.IsNullOrWhiteSpace(request.Priority) ? "Normal" : request.Priority.Trim();
        var matchPriority = Priorities.FirstOrDefault(p => p.Equals(priorityKey, StringComparison.OrdinalIgnoreCase));
        if (matchPriority is null)
        {
            return "Priority must be Normal, High, or Urgent.";
        }
        priority = matchPriority;

        if (start is not null && end is not null && end < start)
        {
            return "End date cannot be before start date.";
        }

        return null;
    }

    private IQueryable<Notice> NoticeGraph()
    {
        return _dbContext.Notices.Include(n => n.Author);
    }

    private IQueryable<Notice> ActiveFeedQuery(DateOnly today)
    {
        return NoticeGraph()
            .AsNoTracking()
            .Where(n => !n.IsArchived)
            .Where(n => n.StartDate == null || n.StartDate <= today)
            .Where(n => n.EndDate == null || n.EndDate >= today);
    }

    private static IEnumerable<Notice> OrderFeed(IEnumerable<Notice> notices)
    {
        return notices
            .OrderBy(n => n.IsArchived)
            .ThenBy(n => PriorityRank(n.Priority))
            .ThenByDescending(n => n.StartDate)
            .ThenByDescending(n => n.PostedAt)
            .ThenByDescending(n => n.NoticeId);
    }

    private static int PriorityRank(string? priority)
    {
        if (string.Equals(priority, "Urgent", StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    private static string? ToLegacyTargetRole(string audience)
    {
        return audience.Equals("Students", StringComparison.OrdinalIgnoreCase) ? "Student" : null;
    }

    private Task<Staff?> LoadAuthorAsync(int authorId, CancellationToken cancellationToken)
    {
        return _dbContext.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StaffId == authorId, cancellationToken);
    }

    private async Task<ActionResult?> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }
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
            Audience = string.IsNullOrWhiteSpace(notice.Audience) ? "Everyone" : notice.Audience,
            TargetRole = notice.TargetRole,
            StartDate = notice.StartDate,
            EndDate = notice.EndDate,
            Priority = string.IsNullOrWhiteSpace(notice.Priority) ? "Normal" : notice.Priority,
            IsArchived = notice.IsArchived,
            ArchivedAt = notice.ArchivedAt,
            PostedAt = notice.PostedAt,
        };
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the announcement.";

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
    public string Audience { get; set; } = "Everyone";
    public string? TargetRole { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Priority { get; set; } = "Normal";
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime PostedAt { get; set; }
}

public class NoticeWriteRequest
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Audience { get; set; } = "Everyone";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Priority { get; set; } = "Normal";
}

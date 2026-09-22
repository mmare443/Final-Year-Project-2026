using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// Operational announcements (not news). Principal publishes and archives.
/// Public/student/staff feeds only include published, non-archived rows
/// inside the optional date window.
/// </summary>
[ApiController]
[Route("api/announcements")]
public class AnnouncementsController : ControllerBase
{
    private static readonly Dictionary<string, string> Audiences = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PUBLIC"] = "PUBLIC",
        ["STUDENT"] = "STUDENT",
        ["STUDENTS"] = "STUDENT",
        ["STAFF"] = "STAFF",
        ["EVERYONE"] = "EVERYONE",
    };

    private static readonly Dictionary<string, string> Priorities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Normal"] = "Normal",
        ["High"] = "High",
        ["Urgent"] = "Urgent",
    };

    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AnnouncementsController(LccCmsDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [AllowAnonymous]
    [HttpGet("public")]
    public async Task<ActionResult<IEnumerable<AnnouncementRecord>>> GetPublic(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _dbContext.Announcements.AsNoTracking()
                .Where(a => a.IsPublished && !a.IsArchived)
                .Where(a => a.Audience == "PUBLIC" || a.Audience == "EVERYONE")
                .Where(a => a.StartDate == null || a.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow))
                .Where(a => a.EndDate == null || a.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                .ToListAsync(cancellationToken);
            return Ok(OrderFeed(rows).Select(ToRecord).ToList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }

    [Authorize]
    [HttpGet("portal")]
    public async Task<ActionResult<IEnumerable<AnnouncementRecord>>> GetPortal(CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            return Unauthorized();
        }

        var student = RoleNames.ToPolicyRole(_currentUser.Role) == RoleNames.Student;
        var query = ActiveFeedQuery(DateOnly.FromDateTime(DateTime.UtcNow));
        query = student
            ? query.Where(a => a.Audience == "STUDENT" || a.Audience == "EVERYONE" || a.Audience == "PUBLIC")
            : query.Where(a => a.Audience == "STAFF" || a.Audience == "EVERYONE" || a.Audience == "PUBLIC");

        var rows = await query.ToListAsync(cancellationToken);
        return Ok(OrderFeed(rows).Select(ToRecord));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AnnouncementRecord>>> GetAll(CancellationToken cancellationToken)
    {
        var rows = await AnnouncementGraph().AsNoTracking().ToListAsync(cancellationToken);
        return Ok(OrderFeed(rows).Select(ToRecord));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AnnouncementRecord>> GetById(int id, CancellationToken cancellationToken)
    {
        var row = await AnnouncementGraph().AsNoTracking()
            .FirstOrDefaultAsync(a => a.AnnouncementId == id, cancellationToken);
        return row is null ? NotFound() : Ok(ToRecord(row));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPost]
    public async Task<ActionResult<AnnouncementRecord>> Create(
        [FromBody] AnnouncementWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateWrite(request, out var audience, out var priority, out var start, out var end);
        if (error is not null) return BadRequest(error);
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.StaffId is null)
        {
            return BadRequest("Sign in as Principal to create an announcement.");
        }

        var row = new Announcement
        {
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Audience = audience,
            StartDate = start,
            EndDate = end,
            Priority = priority,
            IsPublished = false,
            IsArchived = false,
            CreatedBy = _currentUser.StaffId.Value,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Announcements.Add(row);
        var failed = await SaveAsync(cancellationToken);
        if (failed is not null) return failed;
        await LoadAuthorAsync(row, cancellationToken);
        return Ok(ToRecord(row));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<AnnouncementRecord>> Update(
        int id,
        [FromBody] AnnouncementWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateWrite(request, out var audience, out var priority, out var start, out var end);
        if (error is not null) return BadRequest(error);

        var row = await AnnouncementGraph().FirstOrDefaultAsync(a => a.AnnouncementId == id, cancellationToken);
        if (row is null) return NotFound();

        row.Title = request.Title.Trim();
        row.Content = request.Content.Trim();
        row.Audience = audience;
        row.StartDate = start;
        row.EndDate = end;
        row.Priority = priority;
        row.UpdatedAt = DateTime.UtcNow;

        var failed = await SaveAsync(cancellationToken);
        if (failed is not null) return failed;
        return Ok(ToRecord(row));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPut("{id:int}/publish")]
    public Task<ActionResult<AnnouncementRecord>> Publish(int id, CancellationToken cancellationToken)
        => SetFlagsAsync(id, published: true, archived: false, cancellationToken);

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPut("{id:int}/archive")]
    public Task<ActionResult<AnnouncementRecord>> Archive(int id, CancellationToken cancellationToken)
        => SetFlagsAsync(id, published: null, archived: true, cancellationToken);

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPut("{id:int}/restore")]
    public Task<ActionResult<AnnouncementRecord>> Restore(int id, CancellationToken cancellationToken)
        => SetFlagsAsync(id, published: null, archived: false, cancellationToken);

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var row = await _dbContext.Announcements.FirstOrDefaultAsync(a => a.AnnouncementId == id, cancellationToken);
        if (row is null) return NotFound();
        var record = ToRecord(row);
        _dbContext.Announcements.Remove(row);
        var failed = await SaveAsync(cancellationToken);
        if (failed is not null) return failed;
        return Ok(record);
    }

    private async Task<ActionResult<AnnouncementRecord>> SetFlagsAsync(
        int id, bool? published, bool archived, CancellationToken cancellationToken)
    {
        var row = await AnnouncementGraph().FirstOrDefaultAsync(a => a.AnnouncementId == id, cancellationToken);
        if (row is null) return NotFound();
        if (published is bool publish)
        {
            row.IsPublished = publish;
        }
        row.IsArchived = archived;
        row.ArchivedAt = archived ? DateTime.UtcNow : null;
        row.UpdatedAt = DateTime.UtcNow;
        var failed = await SaveAsync(cancellationToken);
        if (failed is not null) return failed;
        return Ok(ToRecord(row));
    }

    private static string? ValidateWrite(
        AnnouncementWriteRequest request,
        out string audience,
        out string priority,
        out DateOnly? start,
        out DateOnly? end)
    {
        audience = "EVERYONE";
        priority = "Normal";
        start = request.StartDate;
        end = request.EndDate;

        if (string.IsNullOrWhiteSpace(request.Title)) return "Title is required.";
        if (request.Title.Trim().Length > 150) return "Title must be 150 characters or fewer.";
        if (string.IsNullOrWhiteSpace(request.Content)) return "Content is required.";

        var audienceKey = string.IsNullOrWhiteSpace(request.Audience) ? "EVERYONE" : request.Audience.Trim();
        if (!Audiences.TryGetValue(audienceKey, out audience!))
        {
            return "Audience must be PUBLIC, STUDENT, STAFF, or EVERYONE.";
        }

        var priorityKey = string.IsNullOrWhiteSpace(request.Priority) ? "Normal" : request.Priority.Trim();
        if (!Priorities.TryGetValue(priorityKey, out priority!))
        {
            return "Priority must be Normal, High, or Urgent.";
        }

        if (start is not null && end is not null && end < start)
        {
            return "End date cannot be before start date.";
        }

        return null;
    }

    private IQueryable<Announcement> AnnouncementGraph()
    {
        return _dbContext.Announcements.Include(a => a.CreatedByNavigation);
    }

    private IQueryable<Announcement> ActiveFeedQuery(DateOnly today)
    {
        return AnnouncementGraph()
            .AsNoTracking()
            .Where(a => a.IsPublished && !a.IsArchived)
            .Where(a => a.StartDate == null || a.StartDate <= today)
            .Where(a => a.EndDate == null || a.EndDate >= today);
    }

    private static IEnumerable<Announcement> OrderFeed(IEnumerable<Announcement> rows)
    {
        return rows
            .OrderBy(a => a.IsArchived)
            .ThenByDescending(a => a.IsPublished)
            .ThenBy(a => PriorityRank(a.Priority))
            .ThenByDescending(a => a.StartDate)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AnnouncementId);
    }

    private static int PriorityRank(string? priority)
    {
        if (string.Equals(priority, "Urgent", StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    private async Task LoadAuthorAsync(Announcement row, CancellationToken cancellationToken)
    {
        row.CreatedByNavigation = await _dbContext.Staff.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StaffId == row.CreatedBy, cancellationToken) ?? row.CreatedByNavigation;
    }

    private async Task<ActionResult?> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException ex) when (TryDescribe(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }
    }

    private static AnnouncementRecord ToRecord(Announcement row)
    {
        return new AnnouncementRecord
        {
            Id = row.AnnouncementId,
            AnnouncementId = row.AnnouncementId,
            Title = row.Title,
            Content = row.Content,
            Audience = row.Audience,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            Priority = row.Priority,
            IsPublished = row.IsPublished,
            IsArchived = row.IsArchived,
            ArchivedAt = row.ArchivedAt,
            CreatedBy = row.CreatedBy,
            CreatedByName = row.CreatedByNavigation?.FullName ?? "",
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };
    }

    private static bool TryDescribe(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the announcement.";
        if (ex.InnerException is SqlException { Number: 547 })
        {
            message = "A related record was not found (author).";
            return true;
        }
        return ex.InnerException is SqlException;
    }

    private static bool IsMissingSchema(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Number is 208 or 207)
            {
                return true;
            }
        }

        return false;
    }
}

public class AnnouncementRecord
{
    public int Id { get; set; }
    public int AnnouncementId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Audience { get; set; } = "EVERYONE";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Priority { get; set; } = "Normal";
    public bool IsPublished { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public int CreatedBy { get; set; }
    public string CreatedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AnnouncementWriteRequest
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Audience { get; set; } = "EVERYONE";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Priority { get; set; } = "Normal";
}

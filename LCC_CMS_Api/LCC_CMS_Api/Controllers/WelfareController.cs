using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M10 Phase 3 — welfare cases and append-only case notes.
/// Students see their own cases. Assigned staff may update those cases
/// and add notes. RegistrarAdmin has full access. Notes are create/read only.
/// </summary>
[ApiController]
[Route("api/welfare-cases")]
public class WelfareController : ControllerBase
{
    public const string StatusOpen = "Open";
    public const string StatusInProgress = "In Progress";
    public const string StatusResolved = "Resolved";

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        StatusOpen,
        StatusInProgress,
        StatusResolved,
    };

    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public WelfareController(LccCmsDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WelfareCaseRecord>>> GetAll(
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        if (actor.Error is not null) return actor.Error;

        var query = CaseQuery();
        if (!actor.IsRegistrar)
        {
            if (actor.IsStudent)
            {
                query = query.Where(c => c.StudentId == actor.StudentId);
            }
            else if (actor.StaffId is int officerId)
            {
                query = query.Where(c => c.AssignedOfficerId == officerId);
            }
            else
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        var cases = await query
            .OrderByDescending(c => c.DateLogged)
            .ThenByDescending(c => c.CaseId)
            .ToListAsync(cancellationToken);
        return Ok(cases);
    }

    [HttpPost]
    public async Task<ActionResult<WelfareCaseRecord>> Create(
        [FromBody] WelfareCaseCreateRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        if (actor.Error is not null) return actor.Error;
        if (!actor.IsRegistrar) return StatusCode(StatusCodes.Status403Forbidden);

        var type = request.CaseType?.Trim() ?? "";
        if (type.Length == 0) return BadRequest("Case type is required.");
        if (type.Length > 50) return BadRequest("Case type must be 50 characters or fewer.");
        if (request.StudentId <= 0) return BadRequest("Student is required.");
        if (request.AssignedOfficerId <= 0) return BadRequest("Assigned officer is required.");

        if (!await _dbContext.Students.AnyAsync(s => s.StudentId == request.StudentId, cancellationToken))
        {
            return BadRequest("Student not found.");
        }

        if (!await _dbContext.Staff.AnyAsync(s => s.StaffId == request.AssignedOfficerId, cancellationToken))
        {
            return BadRequest("Assigned officer not found.");
        }

        var welfareCase = new WelfareCase
        {
            StudentId = request.StudentId,
            AssignedOfficerId = request.AssignedOfficerId,
            CaseType = type,
            Status = StatusOpen,
            DateLogged = DateTime.UtcNow,
            DateResolved = null,
        };
        _dbContext.WelfareCases.Add(welfareCase);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await CaseQuery()
            .FirstAsync(c => c.CaseId == welfareCase.CaseId, cancellationToken);
        return Created($"/api/welfare-cases/{welfareCase.CaseId}", created);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WelfareCaseRecord>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        if (actor.Error is not null) return actor.Error;

        var welfareCase = await CaseQuery()
            .FirstOrDefaultAsync(c => c.CaseId == id, cancellationToken);
        if (welfareCase is null) return NotFound();
        if (!CanView(actor, welfareCase.StudentId, welfareCase.AssignedOfficerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return Ok(welfareCase);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<WelfareCaseRecord>> Update(
        int id,
        [FromBody] WelfareCaseUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        if (actor.Error is not null) return actor.Error;

        var welfareCase = await _dbContext.WelfareCases
            .FirstOrDefaultAsync(c => c.CaseId == id, cancellationToken);
        if (welfareCase is null) return NotFound();
        if (!CanManage(actor, welfareCase.AssignedOfficerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var type = request.CaseType?.Trim() ?? "";
        if (type.Length == 0) return BadRequest("Case type is required.");
        if (type.Length > 50) return BadRequest("Case type must be 50 characters or fewer.");

        var status = request.Status?.Trim() ?? "";
        if (!AllowedStatuses.Contains(status))
        {
            return BadRequest("Status must be Open, In Progress, or Resolved.");
        }

        if (request.AssignedOfficerId <= 0) return BadRequest("Assigned officer is required.");
        if (request.AssignedOfficerId != welfareCase.AssignedOfficerId && !actor.IsRegistrar)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!await _dbContext.Staff.AnyAsync(
                s => s.StaffId == request.AssignedOfficerId, cancellationToken))
        {
            return BadRequest("Assigned officer not found.");
        }

        welfareCase.CaseType = type;
        welfareCase.AssignedOfficerId = request.AssignedOfficerId;
        ApplyStatus(welfareCase, status);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await CaseQuery()
            .FirstAsync(c => c.CaseId == id, cancellationToken);
        return Ok(updated);
    }

    [HttpGet("{id:int}/notes")]
    public async Task<ActionResult<IEnumerable<CaseNoteRecord>>> GetNotes(
        int id,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        if (actor.Error is not null) return actor.Error;

        var welfareCase = await _dbContext.WelfareCases
            .AsNoTracking()
            .Select(c => new { c.CaseId, c.StudentId, c.AssignedOfficerId })
            .FirstOrDefaultAsync(c => c.CaseId == id, cancellationToken);
        if (welfareCase is null) return NotFound();
        if (!CanView(actor, welfareCase.StudentId, welfareCase.AssignedOfficerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var notes = await NoteQuery()
            .Where(n => n.CaseId == id)
            .OrderBy(n => n.CreatedAt)
            .ThenBy(n => n.CaseNoteId)
            .ToListAsync(cancellationToken);
        return Ok(notes);
    }

    [HttpPost("{id:int}/notes")]
    public async Task<ActionResult<CaseNoteRecord>> AddNote(
        int id,
        [FromBody] CaseNoteCreateRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        if (actor.Error is not null) return actor.Error;
        if (actor.StaffId is not int staffId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var welfareCase = await _dbContext.WelfareCases
            .FirstOrDefaultAsync(c => c.CaseId == id, cancellationToken);
        if (welfareCase is null) return NotFound();
        if (!CanManage(actor, welfareCase.AssignedOfficerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var text = request.Note?.Trim() ?? "";
        if (text.Length == 0) return BadRequest("Note is required.");
        if (text.Length > 1000) return BadRequest("Note must be 1000 characters or fewer.");

        var note = new CaseNote
        {
            CaseId = id,
            StaffId = staffId,
            Note = text,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.CaseNotes.Add(note);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await NoteQuery()
            .FirstAsync(n => n.CaseNoteId == note.CaseNoteId, cancellationToken);
        return Created($"/api/welfare-cases/{id}/notes/{note.CaseNoteId}", created);
    }

    private IQueryable<WelfareCaseRecord> CaseQuery()
    {
        return _dbContext.WelfareCases
            .AsNoTracking()
            .Select(c => new WelfareCaseRecord
            {
                CaseId = c.CaseId,
                StudentId = c.StudentId,
                StudentNumber = c.Student.StudentNumber,
                StudentEmail = c.Student.StudentNavigation.Email,
                AssignedOfficerId = c.AssignedOfficerId,
                AssignedOfficerEmail = c.AssignedOfficer.StaffNavigation.Email,
                CaseType = c.CaseType,
                Status = c.Status,
                DateLogged = c.DateLogged,
                DateResolved = c.DateResolved,
            });
    }

    private IQueryable<CaseNoteRecord> NoteQuery()
    {
        return _dbContext.CaseNotes
            .AsNoTracking()
            .Select(n => new CaseNoteRecord
            {
                CaseNoteId = n.CaseNoteId,
                CaseId = n.CaseId,
                StaffId = n.StaffId,
                StaffEmail = n.Staff.StaffNavigation.Email,
                Note = n.Note,
                CreatedAt = n.CreatedAt,
            });
    }

    private async Task<Actor> ResolveActorAsync(CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            return new Actor(Error: Unauthorized());
        }

        var role = RoleNames.ToPolicyRole(_currentUser.Role);
        return new Actor(
            IsRegistrar: role.Equals(RoleNames.RegistrarAdmin, StringComparison.OrdinalIgnoreCase),
            IsStudent: role.Equals(RoleNames.Student, StringComparison.OrdinalIgnoreCase),
            StudentId: _currentUser.StudentId,
            StaffId: _currentUser.StaffId);
    }

    private static bool CanView(Actor actor, int studentId, int assignedOfficerId)
    {
        if (actor.IsRegistrar) return true;
        if (actor.IsStudent && actor.StudentId == studentId) return true;
        if (actor.StaffId == assignedOfficerId) return true;
        return false;
    }

    private static bool CanManage(Actor actor, int assignedOfficerId)
    {
        if (actor.IsRegistrar) return true;
        return actor.StaffId == assignedOfficerId;
    }

    private static void ApplyStatus(WelfareCase welfareCase, string status)
    {
        var wasResolved = string.Equals(welfareCase.Status, StatusResolved, StringComparison.Ordinal);
        welfareCase.Status = status;
        if (status == StatusResolved)
        {
            welfareCase.DateResolved ??= DateTime.UtcNow;
        }
        else if (wasResolved)
        {
            welfareCase.DateResolved = null;
        }
    }

    private sealed record Actor(
        bool IsRegistrar = false,
        bool IsStudent = false,
        int? StudentId = null,
        int? StaffId = null,
        ActionResult? Error = null);
}

public class WelfareCaseCreateRequest
{
    public int StudentId { get; set; }
    public int AssignedOfficerId { get; set; }
    public string CaseType { get; set; } = "";
}

public class WelfareCaseUpdateRequest
{
    public int AssignedOfficerId { get; set; }
    public string CaseType { get; set; } = "";
    public string Status { get; set; } = "";
}

public class WelfareCaseRecord
{
    public int CaseId { get; set; }
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = "";
    public string StudentEmail { get; set; } = "";
    public int AssignedOfficerId { get; set; }
    public string AssignedOfficerEmail { get; set; } = "";
    public string CaseType { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime DateLogged { get; set; }
    public DateTime? DateResolved { get; set; }
}

public class CaseNoteCreateRequest
{
    public string Note { get; set; } = "";
}

public class CaseNoteRecord
{
    public int CaseNoteId { get; set; }
    public int CaseId { get; set; }
    public int StaffId { get; set; }
    public string StaffEmail { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

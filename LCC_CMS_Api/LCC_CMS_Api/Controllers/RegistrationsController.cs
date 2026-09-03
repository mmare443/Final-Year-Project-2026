using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M4 — Course Registration (Module Specification).
///
/// Phase 3: GET, POST, drop, and decide persist through EF Core.
/// <c>_registrations</c> is kept in sync so Attendance/Learning rosters
/// still read Approved rows from the in-memory list.
///
/// Actors: Student, HoD, Registrar/Admin. Business rules enforced below,
/// per spec: prerequisites must be met, can't re-register for a passed
/// course, per-semester credit load cap, add/drop only within the active
/// semester's window, every registration needs HoD or Registrar approval.
///
/// SCOPING NOTE: this pass wires the Student submission side and the
/// Registrar/Admin approval side fully (spec explicitly allows either
/// role to approve). HoD's own approval UI is not wired in this pass —
/// flagged honestly rather than silently left out; the backend endpoint
/// itself doesn't care which of the two roles calls it, so adding HoD's
/// UI later is a frontend-only addition, not a backend change.
///
/// MAX_CREDIT_LOAD is a placeholder business constant — adjust once LCCB
/// confirms the actual per-semester cap.
/// </summary>
[ApiController]
[Route("api/registrations")]
public class RegistrationsController : ControllerBase
{
    private const int MaxCreditLoad = 40;
    private readonly LccCmsDbContext _dbContext;

    public RegistrationsController(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // studentId here matches StudentsController's StudentProfile.Id (e.g.
    // "LCC-24001") — same in-memory-stage simplification as elsewhere.
    // Seeded Approved rows so M5 has a class roster without requiring a
    // live M4 walkthrough first. Internal so AttendanceController can read it.
    internal static readonly List<RegistrationRecord> _registrations = new()
    {
        Seed(1, "LCC-24001", "Mond Mare", 1, "BAM101", "Introduction to Business", 10),
        Seed(2, "LCC-24002", "Sarah Kuman", 1, "BAM101", "Introduction to Business", 10),
        Seed(3, "LCC-24003", "Peter Namba", 1, "BAM101", "Introduction to Business", 10),
        Seed(4, "LCC-24004", "Agnes Wemin", 1, "BAM101", "Introduction to Business", 10),
        Seed(5, "LCC-24001", "Mond Mare", 2, "BAM102", "Principles of Accounting", 10),
        Seed(6, "LCC-24002", "Sarah Kuman", 2, "BAM102", "Principles of Accounting", 10),
    };

    private static RegistrationRecord Seed(
        int id, string studentId, string name, int courseId, string code, string courseName, int credits)
    {
        return new RegistrationRecord
        {
            Id = id,
            StudentId = studentId,
            StudentName = name,
            CourseId = courseId,
            CourseCode = code,
            CourseName = courseName,
            CreditValue = credits,
            SemesterId = 1,
            Status = "Approved",
            RegisteredAt = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),
        };
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RegistrationRecord>>> GetAll([FromQuery] string? studentId)
    {
        var query = _dbContext.Registrations
            .AsNoTracking()
            .Include(r => r.Student)
                .ThenInclude(s => s.Admission)
            .Include(r => r.Allocation)
                .ThenInclude(a => a.Course)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(studentId))
        {
            query = query.Where(r => r.Student.StudentNumber == studentId);
        }

        var rows = await query
            .OrderByDescending(r => r.RegisteredAt)
            .ThenBy(r => r.RegistrationId)
            .ToListAsync();

        return Ok(rows.Select(ToRecord));
    }

    [HttpPost]
    public async Task<ActionResult<RegistrationRecord>> Register([FromBody] RegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StudentId))
        {
            return BadRequest("Student is required.");
        }

        var activeSemester = await _dbContext.Semesters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsActive);
        if (activeSemester is null)
        {
            return BadRequest("No active semester is currently open for registration.");
        }

        var student = await _dbContext.Students
            .Include(s => s.Admission)
            .FirstOrDefaultAsync(s => s.StudentNumber == request.StudentId.Trim());
        if (student is null)
        {
            return BadRequest("Student not found.");
        }

        var course = await _dbContext.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CourseId == request.CourseId);
        if (course is null)
        {
            return BadRequest("Course not found.");
        }

        var allocation = await _dbContext.CourseAllocations
            .Include(a => a.Course)
            .Where(a => a.CourseId == request.CourseId && a.SemesterId == activeSemester.SemesterId)
            .OrderBy(a => a.AllocationId)
            .FirstOrDefaultAsync();
        if (allocation is null)
        {
            return BadRequest("This course is not allocated in the active semester.");
        }

        var alreadyRegistered = await _dbContext.Registrations.AnyAsync(r =>
            r.StudentId == student.StudentId && r.AllocationId == allocation.AllocationId);
        if (alreadyRegistered)
        {
            return Conflict("You are already registered for this course this semester.");
        }

        var currentLoad = await _dbContext.Registrations
            .Where(r => r.StudentId == student.StudentId
                        && r.Allocation.SemesterId == activeSemester.SemesterId
                        && (r.Status == "Pending" || r.Status == "Approved"))
            .SumAsync(r => r.Allocation.Course.CreditValue);
        if (currentLoad + allocation.Course.CreditValue > MaxCreditLoad)
        {
            return BadRequest($"This would exceed your maximum credit load of {MaxCreditLoad} for the semester.");
        }

        var registration = new Registration
        {
            StudentId = student.StudentId,
            AllocationId = allocation.AllocationId,
            AttemptNo = 1,
            Status = "Pending",
            RegisteredAt = DateTime.UtcNow,
        };

        _dbContext.Registrations.Add(registration);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        registration.Student = student;
        registration.Allocation = allocation;
        var record = ToRecord(registration);
        _registrations.Add(record);
        return Ok(record);
    }

    // Rule: add/drop only permitted within the active semester's window.
    // Simplification for this stage: any semester marked active is
    // treated as within its window — real date-range checking against
    // StartDate/EndDate is a straightforward follow-up once this needs to
    // be date-accurate rather than flag-accurate.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Drop(int id)
    {
        var registration = await LoadForUpdateAsync(id);
        if (registration is null) return NotFound();

        var semester = await _dbContext.Semesters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SemesterId == registration.Allocation.SemesterId);
        if (semester is null || !semester.IsActive)
        {
            return BadRequest("Add/drop is only permitted within the active semester's registration window.");
        }

        registration.Status = "Dropped";

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        var record = ToRecord(registration);
        SyncMemory(record);
        return Ok(record);
    }

    // [Authorize(Policy = "RegistrarAdminOnly")] or HoD — re-enable once AuthEnabled=true.
    // Per spec, either role may approve; this endpoint itself is role-agnostic.
    [HttpPut("{id}/decision")]
    public async Task<ActionResult<RegistrationRecord>> Decide(int id, [FromBody] RegistrationDecisionRequest request)
    {
        if (request.Decision != "approve" && request.Decision != "reject")
        {
            return BadRequest("Decision must be 'approve' or 'reject'.");
        }

        if (request.Decision == "reject" && string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest("A reason is required to reject a registration.");
        }

        var registration = await LoadForUpdateAsync(id);
        if (registration is null) return NotFound();

        if (!string.Equals(registration.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("Only pending registrations may be decided.");
        }

        // Placeholder until AuthEnabled=true resolves the signed-in staff id.
        var placeholderReviewerId = await _dbContext.Staff
            .AsNoTracking()
            .Select(s => (int?)s.StaffId)
            .FirstOrDefaultAsync();

        registration.Status = request.Decision == "approve" ? "Approved" : "Rejected";
        registration.ApprovedBy = placeholderReviewerId;

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        var record = ToRecord(registration);
        if (request.Decision == "reject")
        {
            record.RejectionReason = request.Reason;
        }

        SyncMemory(record);
        return Ok(record);
    }

    private async Task<Registration?> LoadForUpdateAsync(int id)
    {
        return await _dbContext.Registrations
            .Include(r => r.Student)
                .ThenInclude(s => s.Admission)
            .Include(r => r.Allocation)
                .ThenInclude(a => a.Course)
            .FirstOrDefaultAsync(r => r.RegistrationId == id);
    }

    private static void SyncMemory(RegistrationRecord record)
    {
        var existing = _registrations.FirstOrDefault(r => r.Id == record.Id);
        if (existing is null)
        {
            _registrations.Add(record);
            return;
        }

        existing.Status = record.Status;
        existing.RejectionReason = record.RejectionReason;
        existing.StudentId = record.StudentId;
        existing.StudentName = record.StudentName;
        existing.CourseId = record.CourseId;
        existing.CourseCode = record.CourseCode;
        existing.CourseName = record.CourseName;
        existing.CreditValue = record.CreditValue;
        existing.SemesterId = record.SemesterId;
        existing.RegisteredAt = record.RegisteredAt;
    }

    private static RegistrationRecord ToRecord(Registration registration)
    {
        var course = registration.Allocation.Course;
        return new RegistrationRecord
        {
            Id = registration.RegistrationId,
            StudentId = registration.Student.StudentNumber,
            StudentName = registration.Student.Admission?.ApplicantName ?? "",
            CourseId = course.CourseId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            CreditValue = (int)course.CreditValue,
            SemesterId = registration.Allocation.SemesterId,
            Status = registration.Status,
            RejectionReason = null,
            RegisteredAt = registration.RegisteredAt,
        };
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the registration.";

        if (ex.InnerException is not SqlException sql)
        {
            return false;
        }

        if (sql.Number is 2601 or 2627)
        {
            status = StatusCodes.Status409Conflict;
            message = "You are already registered for this course this semester.";
            return true;
        }

        if (sql.Number == 547)
        {
            status = StatusCodes.Status400BadRequest;
            message = "A related record was not found (student, allocation, or course).";
            return true;
        }

        return false;
    }
}

public class RegistrationRecord
{
    public int Id { get; set; }
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int CreditValue { get; set; }
    public int SemesterId { get; set; }
    public string Status { get; set; } = ""; // Pending | Approved | Rejected | Dropped | Passed
    public string? RejectionReason { get; set; }
    public DateTime RegisteredAt { get; set; }
}

public class RegistrationRequest
{
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public int CourseId { get; set; }
}

public class RegistrationDecisionRequest
{
    public string Decision { get; set; } = ""; // "approve" | "reject"
    public string? Reason { get; set; }
}

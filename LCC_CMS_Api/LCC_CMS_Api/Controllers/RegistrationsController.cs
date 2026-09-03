using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M4 — Course Registration (Module Specification).
///
/// SKELETON: registrations remain in-memory. Course and semester lookups
/// use EF Core via LccCmsDbContext.
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
    private static int _nextId = 20;

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
    public ActionResult<IEnumerable<RegistrationRecord>> GetAll([FromQuery] string? studentId)
    {
        var results = studentId is null
            ? _registrations
            : _registrations.Where(r => r.StudentId == studentId).ToList();
        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult<RegistrationRecord>> Register([FromBody] RegistrationRequest request)
    {
        var activeSemester = await _dbContext.Semesters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsActive);
        if (activeSemester is null)
        {
            return BadRequest("No active semester is currently open for registration.");
        }

        var course = await _dbContext.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CourseId == request.CourseId);
        if (course is null)
        {
            return BadRequest("Course not found.");
        }

        // Rule: prerequisites must be met (a Passed registration for the
        // prerequisite course must already exist for this student).
        if (course.PrerequisiteCourseId is not null)
        {
            var prereqMet = _registrations.Any(r =>
                r.StudentId == request.StudentId &&
                r.CourseId == course.PrerequisiteCourseId &&
                r.Status == "Passed");
            if (!prereqMet)
            {
                return BadRequest($"Prerequisite not met for {course.CourseCode} — {course.CourseName}.");
            }
        }

        // Rule: cannot register for a course already passed.
        var alreadyPassed = _registrations.Any(r =>
            r.StudentId == request.StudentId && r.CourseId == request.CourseId && r.Status == "Passed");
        if (alreadyPassed)
        {
            return BadRequest("You have already passed this course.");
        }

        // Rule: cannot double-register for the same course in the same
        // semester while a request is pending or approved.
        var alreadyRegistered = _registrations.Any(r =>
            r.StudentId == request.StudentId && r.CourseId == request.CourseId &&
            r.SemesterId == activeSemester.SemesterId && r.Status != "Dropped" && r.Status != "Rejected");
        if (alreadyRegistered)
        {
            return BadRequest("You are already registered for this course this semester.");
        }

        // Rule: per-semester maximum credit/course load.
        var loadCourseIds = _registrations
            .Where(r => r.StudentId == request.StudentId && r.SemesterId == activeSemester.SemesterId &&
                        (r.Status == "Pending" || r.Status == "Approved"))
            .Select(r => r.CourseId)
            .ToList();
        var currentLoad = loadCourseIds.Count == 0
            ? 0m
            : await _dbContext.Courses
                .Where(c => loadCourseIds.Contains(c.CourseId))
                .SumAsync(c => c.CreditValue);
        if (currentLoad + course.CreditValue > MaxCreditLoad)
        {
            return BadRequest($"This would exceed your maximum credit load of {MaxCreditLoad} for the semester.");
        }

        var registration = new RegistrationRecord
        {
            Id = _nextId++,
            StudentId = request.StudentId,
            StudentName = request.StudentName,
            CourseId = course.CourseId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            CreditValue = (int)course.CreditValue,
            SemesterId = activeSemester.SemesterId,
            Status = "Pending",
            RejectionReason = null,
            RegisteredAt = DateTime.UtcNow,
        };
        _registrations.Add(registration);
        return Ok(registration);
    }

    // Rule: add/drop only permitted within the active semester's window.
    // Simplification for this stage: any semester marked active is
    // treated as within its window — real date-range checking against
    // StartDate/EndDate is a straightforward follow-up once this needs to
    // be date-accurate rather than flag-accurate.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Drop(int id)
    {
        var registration = _registrations.FirstOrDefault(r => r.Id == id);
        if (registration is null) return NotFound();

        var semester = await _dbContext.Semesters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SemesterId == registration.SemesterId);
        if (semester is null || !semester.IsActive)
        {
            return BadRequest("Add/drop is only permitted within the active semester's registration window.");
        }

        registration.Status = "Dropped";
        return Ok(registration);
    }

    // [Authorize(Policy = "RegistrarAdminOnly")] or HoD — re-enable once AuthEnabled=true.
    // Per spec, either role may approve; this endpoint itself is role-agnostic.
    [HttpPut("{id}/decision")]
    public ActionResult<RegistrationRecord> Decide(int id, [FromBody] RegistrationDecisionRequest request)
    {
        var registration = _registrations.FirstOrDefault(r => r.Id == id);
        if (registration is null) return NotFound();

        if (request.Decision == "approve")
        {
            registration.Status = "Approved";
            registration.RejectionReason = null;
        }
        else if (request.Decision == "reject")
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest("A reason is required to reject a registration.");
            }
            registration.Status = "Rejected";
            registration.RejectionReason = request.Reason;
        }
        else
        {
            return BadRequest("Decision must be 'approve' or 'reject'.");
        }

        return Ok(registration);
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

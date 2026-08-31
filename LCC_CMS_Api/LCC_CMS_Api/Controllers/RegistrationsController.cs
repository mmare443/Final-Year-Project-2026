using Microsoft.AspNetCore.Mvc;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M4 — Course Registration (Module Specification).
///
/// SKELETON: in-memory, same pattern as the rest of this project. Reads
/// M3's static data directly (AcademicStructureController._courses etc.)
/// — see that controller's header comment for why this is a deliberate
/// in-memory-stage simplification, not a real foreign key yet.
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

    // studentId here matches StudentsController's StudentProfile.Id (e.g.
    // "LCC-24001") — same in-memory-stage simplification as elsewhere.
    // Seeded Approved rows so M5 has a class roster without requiring a
    // live M4 walkthrough first. Internal so AttendanceController can read it.
    internal static readonly List<RegistrationRecord> _registrations = new()
    {
        Seed(1, "LCC-24001", "Mond Mare", 1),
        Seed(2, "LCC-24002", "Sarah Kuman", 1),
        Seed(3, "LCC-24003", "Peter Namba", 1),
        Seed(4, "LCC-24004", "Agnes Wemin", 1),
        Seed(5, "LCC-24001", "Mond Mare", 2),
        Seed(6, "LCC-24002", "Sarah Kuman", 2),
    };
    private static int _nextId = 20;

    private static RegistrationRecord Seed(int id, string studentId, string name, int courseId)
    {
        var course = AcademicStructureController._courses.First(c => c.Id == courseId);
        return new RegistrationRecord
        {
            Id = id,
            StudentId = studentId,
            StudentName = name,
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            CreditValue = course.CreditValue,
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
    public ActionResult<RegistrationRecord> Register([FromBody] RegistrationRequest request)
    {
        var activeSemester = AcademicStructureController._semesters.FirstOrDefault(s => s.IsActive);
        if (activeSemester is null)
        {
            return BadRequest("No active semester is currently open for registration.");
        }

        var course = AcademicStructureController._courses.FirstOrDefault(c => c.Id == request.CourseId);
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
                return BadRequest($"Prerequisite not met for {course.Code} — {course.Name}.");
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
            r.SemesterId == activeSemester.Id && r.Status != "Dropped" && r.Status != "Rejected");
        if (alreadyRegistered)
        {
            return BadRequest("You are already registered for this course this semester.");
        }

        // Rule: per-semester maximum credit/course load.
        var currentLoad = _registrations
            .Where(r => r.StudentId == request.StudentId && r.SemesterId == activeSemester.Id &&
                        (r.Status == "Pending" || r.Status == "Approved"))
            .Sum(r => AcademicStructureController._courses.FirstOrDefault(c => c.Id == r.CourseId)?.CreditValue ?? 0);
        if (currentLoad + course.CreditValue > MaxCreditLoad)
        {
            return BadRequest($"This would exceed your maximum credit load of {MaxCreditLoad} for the semester.");
        }

        var registration = new RegistrationRecord
        {
            Id = _nextId++,
            StudentId = request.StudentId,
            StudentName = request.StudentName,
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            CreditValue = course.CreditValue,
            SemesterId = activeSemester.Id,
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
    public IActionResult Drop(int id)
    {
        var registration = _registrations.FirstOrDefault(r => r.Id == id);
        if (registration is null) return NotFound();

        var semester = AcademicStructureController._semesters.FirstOrDefault(s => s.Id == registration.SemesterId);
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

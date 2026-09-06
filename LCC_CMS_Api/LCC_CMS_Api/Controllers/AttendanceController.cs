using System.Globalization;
using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M5 — Attendance Management (Module Specification).
///
/// Sessions, marks, rates, reports, and alerts are EF-backed or derived.
/// Alerts are computed from rates below 75%; they are not stored.
///
/// Actors: Lecturer (mark sessions), Student (own rates + alerts),
/// HoD (monitoring dashboard and reports by unit or by student).
///
/// Business rule: an alert fires automatically the moment a student's
/// running rate for a course falls below 75%. Present, Late, and
/// Excused count as attended; Absent does not. Spec entities:
/// attendance_sessions C/R, attendances C/R/U — no session delete.
///
/// [Authorize(Policy = "LecturerOnly")] on write endpoints once
/// AuthEnabled=true. HoD/Student reads are role-scoped in the SPA today.
/// </summary>
[ApiController]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    public const decimal ThresholdPercent = 75m;
    private static readonly string[] AllowedStatuses = { "Present", "Absent", "Late", "Excused" };

    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AttendanceController(LccCmsDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<AttendanceSessionRecord>>> GetSessions([FromQuery] int? allocationId)
    {
        var query = SessionGraph();
        if (allocationId is not null)
        {
            query = query.Where(s => s.AllocationId == allocationId);
        }

        var sessions = await query
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync();

        return Ok(sessions.Select(ToSessionRecord));
    }

    [HttpGet("sessions/{id}")]
    public async Task<ActionResult<AttendanceSessionDetail>> GetSession(int id)
    {
        var session = await SessionGraph().FirstOrDefaultAsync(s => s.SessionId == id);
        if (session is null) return NotFound();

        var roster = await RosterFor(session.AllocationId);
        var marks = await MarksForSessionAsync(id);
        return Ok(new AttendanceSessionDetail
        {
            Session = ToSessionRecord(session),
            Roster = roster,
            Marks = marks,
        });
    }

    [HttpGet("roster")]
    public async Task<ActionResult<IEnumerable<AttendanceRosterStudent>>> GetRoster([FromQuery] int allocationId)
    {
        return Ok(await RosterFor(allocationId));
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpPost("sessions")]
    public async Task<ActionResult<AttendanceSessionRecord>> OpenSession([FromBody] OpenSessionRequest request)
    {
        var allocation = await LoadAllocation(request.AllocationId);
        if (allocation is null) return BadRequest("Course allocation not found.");

        if (string.IsNullOrWhiteSpace(request.SessionDate))
        {
            return BadRequest("Session date is required.");
        }

        if (!DateOnly.TryParse(request.SessionDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sessionDate))
        {
            return BadRequest("Session date must be a valid date.");
        }

        var exists = await _dbContext.AttendanceSessions.AnyAsync(s =>
            s.AllocationId == request.AllocationId && s.SessionDate == sessionDate);
        if (exists)
        {
            return Conflict("A session already exists for this class on that date.");
        }

        var session = new AttendanceSession
        {
            AllocationId = allocation.AllocationId,
            SessionDate = sessionDate,
        };
        _dbContext.AttendanceSessions.Add(session);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(new AttendanceSessionRecord
        {
            Id = session.SessionId,
            AllocationId = allocation.AllocationId,
            CourseId = allocation.Course.CourseId,
            CourseCode = allocation.Course.CourseCode,
            CourseName = allocation.Course.CourseName,
            LecturerName = LecturerName(allocation),
            SessionDate = sessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        });
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpPut("sessions/{id}/marks")]
    public async Task<ActionResult<AttendanceSessionDetail>> SaveMarks(int id, [FromBody] SaveMarksRequest request)
    {
        var session = await _dbContext.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == id);
        if (session is null) return NotFound();

        if (request.Marks is null || request.Marks.Count == 0)
        {
            return BadRequest("At least one attendance mark is required.");
        }

        foreach (var entry in request.Marks)
        {
            if (string.IsNullOrWhiteSpace(entry.StudentId))
            {
                return BadRequest("Student is required for each mark.");
            }

            if (!AllowedStatuses.Contains(entry.Status))
            {
                return BadRequest("Status must be Present, Absent, Late, or Excused.");
            }
        }

        var numbers = request.Marks.Select(m => m.StudentId.Trim()).Distinct().ToList();
        var students = await _dbContext.Students
            .Where(s => numbers.Contains(s.StudentNumber))
            .ToListAsync();
        if (students.Count != numbers.Count)
        {
            return BadRequest("One or more students were not found.");
        }

        var byNumber = students.ToDictionary(s => s.StudentNumber, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in request.Marks)
        {
            var student = byNumber[entry.StudentId.Trim()];
            var existing = await _dbContext.Attendances
                .FirstOrDefaultAsync(a => a.SessionId == id && a.StudentId == student.StudentId);
            if (existing is null)
            {
                _dbContext.Attendances.Add(new Attendance
                {
                    SessionId = id,
                    StudentId = student.StudentId,
                    Status = entry.Status,
                });
            }
            else
            {
                existing.Status = entry.Status;
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return await GetSession(id);
    }

    [HttpGet("rates")]
    public async Task<ActionResult<IEnumerable<AttendanceRateRecord>>> GetRates(
        [FromQuery] string? studentId,
        [FromQuery] int? allocationId)
    {
        return Ok(await LoadRatesAsync(studentId, allocationId));
    }

    [Authorize(Policy = "HoDOnly")]
    [HttpGet("alerts")]
    public async Task<ActionResult<IEnumerable<AttendanceAlertRecord>>> GetAlerts(
        [FromQuery] string? studentId,
        CancellationToken cancellationToken)
    {
        var departmentId = await ResolveHoDDepartmentIdAsync(cancellationToken);
        if (departmentId is null) return Unauthorized();

        var rates = await LoadRatesAsync(studentId, null, departmentId);
        var alerts = rates
            .Where(r => r.BelowThreshold)
            .Select(ToAlert)
            .OrderBy(a => a.RatePercent)
            .ThenBy(a => a.StudentName)
            .ToList();
        return Ok(alerts);
    }

    [Authorize(Policy = "HoDOnly")]
    [HttpGet("reports")]
    public async Task<ActionResult<IEnumerable<AttendanceRateRecord>>> GetReports(
        [FromQuery] string view,
        [FromQuery] int? allocationId,
        [FromQuery] string? studentId,
        CancellationToken cancellationToken)
    {
        var departmentId = await ResolveHoDDepartmentIdAsync(cancellationToken);
        if (departmentId is null) return Unauthorized();

        if (view == "unit")
        {
            if (allocationId is null) return BadRequest("allocationId is required for a unit report.");

            var allocationIsInDepartment = await _dbContext.CourseAllocations
                .AsNoTracking()
                .AnyAsync(
                    a => a.AllocationId == allocationId
                        && a.Staff.DepartmentId == departmentId.Value,
                    cancellationToken);
            if (!allocationIsInDepartment) return Forbid();

            return Ok(await LoadRatesAsync(null, allocationId, departmentId));
        }

        if (view == "student")
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return BadRequest("studentId is required for a student report.");
            }
            return Ok(await LoadRatesAsync(studentId, null, departmentId));
        }

        return BadRequest("view must be 'unit' or 'student'.");
    }

    private async Task<CourseAllocation?> LoadAllocation(int allocationId)
    {
        return await _dbContext.CourseAllocations
            .AsNoTracking()
            .Include(a => a.Course)
            .Include(a => a.Staff)
                .ThenInclude(s => s.StaffNavigation)
            .FirstOrDefaultAsync(a => a.AllocationId == allocationId);
    }

    private static string LecturerName(CourseAllocation allocation)
    {
        return allocation.Staff?.StaffNavigation?.Email
            ?? allocation.Staff?.JobTitle
            ?? "";
    }

    private async Task<List<AttendanceRosterStudent>> RosterFor(int allocationId)
    {
        var roster = await _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.AllocationId == allocationId && r.Status == "Approved")
            .Select(r => new AttendanceRosterStudent
            {
                StudentId = r.Student.StudentNumber,
                StudentName = r.Student.Admission != null
                    ? r.Student.Admission.ApplicantName
                    : r.Student.StudentNumber,
            })
            .ToListAsync();

        return roster
            .DistinctBy(s => s.StudentId)
            .OrderBy(s => s.StudentName)
            .ToList();
    }

    private async Task<List<AttendanceMarkRecord>> MarksForSessionAsync(int sessionId)
    {
        var marks = await _dbContext.Attendances
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .Include(a => a.Student)
                .ThenInclude(s => s.Admission)
            .ToListAsync();

        return marks.Select(ToMarkRecord).ToList();
    }

    private static AttendanceMarkRecord ToMarkRecord(Attendance mark)
    {
        return new AttendanceMarkRecord
        {
            Id = mark.AttendanceId,
            SessionId = mark.SessionId,
            StudentId = mark.Student.StudentNumber,
            StudentName = mark.Student.Admission?.ApplicantName ?? mark.Student.StudentNumber,
            Status = mark.Status,
        };
    }

    private IQueryable<AttendanceSession> SessionGraph()
    {
        return _dbContext.AttendanceSessions
            .AsNoTracking()
            .Include(s => s.Allocation)
                .ThenInclude(a => a.Course)
            .Include(s => s.Allocation)
                .ThenInclude(a => a.Staff)
                    .ThenInclude(st => st.StaffNavigation);
    }

    private async Task<List<AttendanceRateRecord>> LoadRatesAsync(
        string? studentNumber,
        int? allocationId,
        int? departmentId = null)
    {
        var query = _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.Status == "Approved");

        if (allocationId is not null)
        {
            query = query.Where(r => r.AllocationId == allocationId);
        }

        if (departmentId is not null)
        {
            query = query.Where(r => r.Allocation.Staff.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(studentNumber))
        {
            query = query.Where(r => r.Student.StudentNumber == studentNumber);
        }

        var rows = await query
            .Select(r => new
            {
                r.AllocationId,
                CourseCode = r.Allocation.Course.CourseCode,
                CourseName = r.Allocation.Course.CourseName,
                LecturerEmail = r.Allocation.Staff.StaffNavigation != null
                    ? r.Allocation.Staff.StaffNavigation.Email
                    : null,
                LecturerTitle = r.Allocation.Staff.JobTitle,
                StudentNumber = r.Student.StudentNumber,
                StudentName = r.Student.Admission != null
                    ? r.Student.Admission.ApplicantName
                    : r.Student.StudentNumber,
                Marked = _dbContext.Attendances.Count(a =>
                    a.StudentId == r.StudentId && a.Session.AllocationId == r.AllocationId),
                Attended = _dbContext.Attendances.Count(a =>
                    a.StudentId == r.StudentId
                    && a.Session.AllocationId == r.AllocationId
                    && a.Status != "Absent"),
            })
            .ToListAsync();

        return rows
            .DistinctBy(r => (r.AllocationId, r.StudentNumber))
            .Select(r =>
            {
                var ratePercent = r.Marked == 0 ? 0m : Math.Round(r.Attended * 100m / r.Marked, 1);
                return new AttendanceRateRecord
                {
                    AllocationId = r.AllocationId,
                    CourseCode = r.CourseCode,
                    CourseName = r.CourseName,
                    LecturerName = r.LecturerEmail ?? r.LecturerTitle ?? "",
                    StudentId = r.StudentNumber,
                    StudentName = r.StudentName,
                    SessionsMarked = r.Marked,
                    SessionsAttended = r.Attended,
                    RatePercent = ratePercent,
                    BelowThreshold = r.Marked > 0 && ratePercent < ThresholdPercent,
                };
            })
            .OrderBy(r => r.StudentName)
            .ThenBy(r => r.CourseCode)
            .ToList();
    }

    private async Task<int?> ResolveHoDDepartmentIdAsync(CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken)
            || _currentUser.UserId is not int
            || _currentUser.StaffId is not int staffId)
        {
            return null;
        }

        return await _dbContext.Staff
            .AsNoTracking()
            .Where(s => s.StaffId == staffId)
            .Select(s => (int?)s.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static AttendanceSessionRecord ToSessionRecord(AttendanceSession session)
    {
        var allocation = session.Allocation;
        return new AttendanceSessionRecord
        {
            Id = session.SessionId,
            AllocationId = session.AllocationId,
            CourseId = allocation?.Course?.CourseId ?? 0,
            CourseCode = allocation?.Course?.CourseCode ?? "",
            CourseName = allocation?.Course?.CourseName ?? "",
            LecturerName = allocation is null ? "" : LecturerName(allocation),
            SessionDate = session.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    private static AttendanceAlertRecord ToAlert(AttendanceRateRecord rate)
    {
        return new AttendanceAlertRecord
        {
            Id = HashCode.Combine(rate.AllocationId, rate.StudentId) & 0x7fffffff,
            AllocationId = rate.AllocationId,
            CourseCode = rate.CourseCode,
            CourseName = rate.CourseName,
            StudentId = rate.StudentId,
            StudentName = rate.StudentName,
            RatePercent = rate.RatePercent,
            AlertedAt = default,
            IsActive = true,
        };
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the attendance session.";

        if (ex.InnerException is not SqlException sql)
        {
            return false;
        }

        if (sql.Number is 2601 or 2627)
        {
            status = StatusCodes.Status409Conflict;
            var detail = sql.Message;
            if (detail.Contains("attendance_sessions", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("UQ_attendance_sessions", StringComparison.OrdinalIgnoreCase))
            {
                message = "A session already exists for this class on that date.";
            }
            else
            {
                message = "This attendance mark already exists.";
            }

            return true;
        }

        if (sql.Number == 547)
        {
            status = StatusCodes.Status400BadRequest;
            message = "A related record was not found (session, student, or course allocation).";
            return true;
        }

        return false;
    }
}

public class AttendanceSessionRecord
{
    public int Id { get; set; }
    public int AllocationId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string LecturerName { get; set; } = "";
    public string SessionDate { get; set; } = "";
}

public class AttendanceMarkRecord
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Status { get; set; } = "";
}

public class AttendanceRosterStudent
{
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
}

public class AttendanceSessionDetail
{
    public AttendanceSessionRecord Session { get; set; } = new();
    public List<AttendanceRosterStudent> Roster { get; set; } = new();
    public List<AttendanceMarkRecord> Marks { get; set; } = new();
}

public class OpenSessionRequest
{
    public int AllocationId { get; set; }
    public string SessionDate { get; set; } = "";
}

public class SaveMarksRequest
{
    public List<AttendanceMarkEntry> Marks { get; set; } = new();
}

public class AttendanceMarkEntry
{
    public string StudentId { get; set; } = "";
    public string Status { get; set; } = "";
}

public class AttendanceRateRecord
{
    public int AllocationId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string LecturerName { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public int SessionsMarked { get; set; }
    public int SessionsAttended { get; set; }
    public decimal RatePercent { get; set; }
    public bool BelowThreshold { get; set; }
}

public class AttendanceAlertRecord
{
    public int Id { get; set; }
    public int AllocationId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public decimal RatePercent { get; set; }
    public DateTime AlertedAt { get; set; }
    public bool IsActive { get; set; }
}

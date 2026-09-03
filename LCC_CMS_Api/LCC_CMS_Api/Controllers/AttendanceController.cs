using System.Globalization;
using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M5 — Attendance Management (Module Specification).
///
/// SKELETON: sessions persist through EF Core. Marks and alerts remain
/// in-memory. The Approved roster is read from registrations.
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

    private static readonly List<AttendanceMarkRecord> _marks = new();
    private static readonly List<AttendanceAlertRecord> _alerts = new();
    private static int _nextMarkId = 1;
    private static int _nextAlertId = 1;
    private static bool _seeded;
    private readonly LccCmsDbContext _dbContext;

    public AttendanceController(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
        EnsureSeed();
    }

    // Two demo sessions on BAM101 so HoD/student screens are not empty
    // on first load. Rates: Mare 100%, Kuman 50% (alert), Namba 0% (alert),
    // Wemin 100% (Late counts as attended).
    private static void EnsureSeed()
    {
        if (_seeded) return;
        _seeded = true;

        SeedMark(1, "LCC-24001", "Mond Mare", "Present");
        SeedMark(1, "LCC-24002", "Sarah Kuman", "Present");
        SeedMark(1, "LCC-24003", "Peter Namba", "Absent");
        SeedMark(1, "LCC-24004", "Agnes Wemin", "Present");

        SeedMark(2, "LCC-24001", "Mond Mare", "Present");
        SeedMark(2, "LCC-24002", "Sarah Kuman", "Absent");
        SeedMark(2, "LCC-24003", "Peter Namba", "Absent");
        SeedMark(2, "LCC-24004", "Agnes Wemin", "Late");

        RecalculateAlertsFromSeed(1, "BAM101", "Introduction to Business");
    }

    private static void SeedMark(int sessionId, string studentId, string studentName, string status)
    {
        _marks.Add(new AttendanceMarkRecord
        {
            Id = _nextMarkId++,
            SessionId = sessionId,
            StudentId = studentId,
            StudentName = studentName,
            Status = status,
        });
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
        var marks = _marks.Where(m => m.SessionId == id).ToList();
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
            if (!AllowedStatuses.Contains(entry.Status))
            {
                return BadRequest("Status must be Present, Absent, Late, or Excused.");
            }

            var existing = _marks.FirstOrDefault(m => m.SessionId == id && m.StudentId == entry.StudentId);
            var name = await StudentDirectory.DisplayNameAsync(_dbContext, entry.StudentId);
            if (existing is null)
            {
                _marks.Add(new AttendanceMarkRecord
                {
                    Id = _nextMarkId++,
                    SessionId = id,
                    StudentId = entry.StudentId,
                    StudentName = name,
                    Status = entry.Status,
                });
            }
            else
            {
                existing.Status = entry.Status;
                existing.StudentName = name;
            }
        }

        await RecalculateAlerts(session.AllocationId);
        return await GetSession(id);
    }

    [HttpGet("rates")]
    public async Task<ActionResult<IEnumerable<AttendanceRateRecord>>> GetRates(
        [FromQuery] string? studentId,
        [FromQuery] int? allocationId)
    {
        var query = _dbContext.CourseAllocations
            .AsNoTracking()
            .Include(a => a.Course)
            .Include(a => a.Staff)
                .ThenInclude(s => s.StaffNavigation)
            .AsQueryable();
        if (allocationId is not null)
        {
            query = query.Where(a => a.AllocationId == allocationId);
        }

        var allocations = await query.ToListAsync();

        var rates = new List<AttendanceRateRecord>();
        foreach (var allocation in allocations)
        {
            var roster = await RosterFor(allocation.AllocationId);
            var sessionIds = await SessionIdsForAllocationAsync(allocation.AllocationId);
            foreach (var student in roster)
            {
                if (studentId is not null && student.StudentId != studentId) continue;
                rates.Add(ComputeRate(allocation, student.StudentId, student.StudentName, sessionIds));
            }
        }

        return Ok(rates);
    }

    [HttpGet("alerts")]
    public ActionResult<IEnumerable<AttendanceAlertRecord>> GetAlerts([FromQuery] string? studentId)
    {
        var results = _alerts.Where(a => a.IsActive);
        if (studentId is not null)
        {
            results = results.Where(a => a.StudentId == studentId);
        }
        return Ok(results.OrderByDescending(a => a.AlertedAt));
    }

    [HttpGet("reports")]
    public async Task<ActionResult<IEnumerable<AttendanceRateRecord>>> GetReports(
        [FromQuery] string view,
        [FromQuery] int? allocationId,
        [FromQuery] string? studentId)
    {
        if (view == "unit")
        {
            if (allocationId is null) return BadRequest("allocationId is required for a unit report.");
            return await GetRates(null, allocationId);
        }

        if (view == "student")
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return BadRequest("studentId is required for a student report.");
            }
            return await GetRates(studentId, null);
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

    private async Task<HashSet<int>> SessionIdsForAllocationAsync(int allocationId)
    {
        var ids = await _dbContext.AttendanceSessions
            .AsNoTracking()
            .Where(s => s.AllocationId == allocationId)
            .Select(s => s.SessionId)
            .ToListAsync();
        return ids.ToHashSet();
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

    private AttendanceRateRecord ComputeRate(
        CourseAllocation allocation,
        string studentId,
        string studentName,
        HashSet<int> sessionIds)
    {
        var studentMarks = _marks.Where(m => sessionIds.Contains(m.SessionId) && m.StudentId == studentId).ToList();
        var total = studentMarks.Count;
        var attended = studentMarks.Count(m => m.Status != "Absent");
        var rate = total == 0 ? 0m : Math.Round(attended * 100m / total, 1);

        return new AttendanceRateRecord
        {
            AllocationId = allocation.AllocationId,
            CourseCode = allocation.Course.CourseCode,
            CourseName = allocation.Course.CourseName,
            LecturerName = LecturerName(allocation),
            StudentId = studentId,
            StudentName = studentName,
            SessionsMarked = total,
            SessionsAttended = attended,
            RatePercent = rate,
            BelowThreshold = total > 0 && rate < ThresholdPercent,
        };
    }

    private static void RecalculateAlertsFromSeed(int allocationId, string courseCode, string courseName)
    {
        var roster = RegistrationsController._registrations
            .Where(r => r.CourseId == 1 && r.SemesterId == 1 && r.Status == "Approved")
            .Select(r => new AttendanceRosterStudent { StudentId = r.StudentId, StudentName = r.StudentName })
            .DistinctBy(s => s.StudentId)
            .ToList();

        foreach (var student in roster)
        {
            var sessionIds = _marks.Select(m => m.SessionId).ToHashSet();
            var studentMarks = _marks.Where(m => sessionIds.Contains(m.SessionId) && m.StudentId == student.StudentId).ToList();
            var total = studentMarks.Count;
            var attended = studentMarks.Count(m => m.Status != "Absent");
            var rate = total == 0 ? 0m : Math.Round(attended * 100m / total, 1);
            var below = total > 0 && rate < ThresholdPercent;

            if (!below) continue;

            _alerts.Add(new AttendanceAlertRecord
            {
                Id = _nextAlertId++,
                AllocationId = allocationId,
                CourseCode = courseCode,
                CourseName = courseName,
                StudentId = student.StudentId,
                StudentName = student.StudentName,
                RatePercent = rate,
                AlertedAt = DateTime.UtcNow,
                IsActive = true,
            });
        }
    }

    private async Task RecalculateAlerts(int allocationId)
    {
        var allocation = await LoadAllocation(allocationId);
        if (allocation is null) return;

        var roster = await RosterFor(allocationId);
        var sessionIds = await SessionIdsForAllocationAsync(allocationId);
        foreach (var student in roster)
        {
            var rate = ComputeRate(allocation, student.StudentId, student.StudentName, sessionIds);
            var existing = _alerts.FirstOrDefault(a =>
                a.AllocationId == allocationId && a.StudentId == student.StudentId);

            if (rate.BelowThreshold)
            {
                if (existing is null)
                {
                    _alerts.Add(new AttendanceAlertRecord
                    {
                        Id = _nextAlertId++,
                        AllocationId = allocationId,
                        CourseCode = rate.CourseCode,
                        CourseName = rate.CourseName,
                        StudentId = student.StudentId,
                        StudentName = student.StudentName,
                        RatePercent = rate.RatePercent,
                        AlertedAt = DateTime.UtcNow,
                        IsActive = true,
                    });
                }
                else
                {
                    existing.RatePercent = rate.RatePercent;
                    existing.IsActive = true;
                    existing.CourseCode = rate.CourseCode;
                    existing.CourseName = rate.CourseName;
                }
            }
            else if (existing is not null)
            {
                existing.IsActive = false;
                existing.RatePercent = rate.RatePercent;
            }
        }
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
            message = "A session already exists for this class on that date.";
            return true;
        }

        if (sql.Number == 547)
        {
            status = StatusCodes.Status400BadRequest;
            message = "A related record was not found (course allocation).";
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

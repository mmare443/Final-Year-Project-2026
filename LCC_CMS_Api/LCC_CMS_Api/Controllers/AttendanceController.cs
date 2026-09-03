using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M5 — Attendance Management (Module Specification).
///
/// SKELETON: in-memory sessions/marks/alerts. Course allocations and
/// courses are read through EF Core. Approved registrations (M4) and
/// student names (M2) still come from sibling controllers' internal lists.
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

    private static readonly List<AttendanceSessionRecord> _sessions = new();
    private static readonly List<AttendanceMarkRecord> _marks = new();
    private static readonly List<AttendanceAlertRecord> _alerts = new();
    private static int _nextSessionId = 1;
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

        var session1 = NewSession(1, "2026-03-03");
        var session2 = NewSession(1, "2026-03-10");
        _sessions.Add(session1);
        _sessions.Add(session2);

        SeedMark(session1.Id, "LCC-24001", "Mond Mare", "Present");
        SeedMark(session1.Id, "LCC-24002", "Sarah Kuman", "Present");
        SeedMark(session1.Id, "LCC-24003", "Peter Namba", "Absent");
        SeedMark(session1.Id, "LCC-24004", "Agnes Wemin", "Present");

        SeedMark(session2.Id, "LCC-24001", "Mond Mare", "Present");
        SeedMark(session2.Id, "LCC-24002", "Sarah Kuman", "Absent");
        SeedMark(session2.Id, "LCC-24003", "Peter Namba", "Absent");
        SeedMark(session2.Id, "LCC-24004", "Agnes Wemin", "Late");

        RecalculateAlertsFromSeed(1, "BAM101", "Introduction to Business");
    }

    private static AttendanceSessionRecord NewSession(int allocationId, string date)
    {
        return new AttendanceSessionRecord
        {
            Id = _nextSessionId++,
            AllocationId = allocationId,
            CourseId = 1,
            CourseCode = "BAM101",
            CourseName = "Introduction to Business",
            LecturerName = "Mr. J. Kaupa",
            SessionDate = date,
        };
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
    public ActionResult<IEnumerable<AttendanceSessionRecord>> GetSessions([FromQuery] int? allocationId)
    {
        var results = allocationId is null
            ? _sessions
            : _sessions.Where(s => s.AllocationId == allocationId).ToList();
        return Ok(results.OrderByDescending(s => s.SessionDate));
    }

    [HttpGet("sessions/{id}")]
    public async Task<ActionResult<AttendanceSessionDetail>> GetSession(int id)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == id);
        if (session is null) return NotFound();

        var roster = await RosterFor(session.AllocationId);
        var marks = _marks.Where(m => m.SessionId == id).ToList();
        return Ok(new AttendanceSessionDetail
        {
            Session = session,
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

        var exists = _sessions.Any(s =>
            s.AllocationId == request.AllocationId && s.SessionDate == request.SessionDate);
        if (exists)
        {
            return BadRequest("A session already exists for this class on that date.");
        }

        var session = new AttendanceSessionRecord
        {
            Id = _nextSessionId++,
            AllocationId = allocation.AllocationId,
            CourseId = allocation.Course.CourseId,
            CourseCode = allocation.Course.CourseCode,
            CourseName = allocation.Course.CourseName,
            LecturerName = LecturerName(allocation),
            SessionDate = request.SessionDate,
        };
        _sessions.Add(session);
        return Ok(session);
    }

    [HttpPut("sessions/{id}/marks")]
    public async Task<ActionResult<AttendanceSessionDetail>> SaveMarks(int id, [FromBody] SaveMarksRequest request)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == id);
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
            var roster = RosterFor(allocation);
            foreach (var student in roster)
            {
                if (studentId is not null && student.StudentId != studentId) continue;
                rates.Add(ComputeRate(allocation, student.StudentId, student.StudentName));
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
        var allocation = await _dbContext.CourseAllocations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AllocationId == allocationId);
        if (allocation is null) return new List<AttendanceRosterStudent>();
        return RosterFor(allocation);
    }

    private static List<AttendanceRosterStudent> RosterFor(CourseAllocation allocation)
    {
        return RegistrationsController._registrations
            .Where(r => r.CourseId == allocation.CourseId &&
                        r.SemesterId == allocation.SemesterId &&
                        r.Status == "Approved")
            .Select(r => new AttendanceRosterStudent
            {
                StudentId = r.StudentId,
                StudentName = r.StudentName,
            })
            .DistinctBy(s => s.StudentId)
            .OrderBy(s => s.StudentName)
            .ToList();
    }

    private AttendanceRateRecord ComputeRate(CourseAllocation allocation, string studentId, string studentName)
    {
        var sessionIds = _sessions.Where(s => s.AllocationId == allocation.AllocationId).Select(s => s.Id).ToHashSet();
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
            var sessionIds = _sessions.Where(s => s.AllocationId == allocationId).Select(s => s.Id).ToHashSet();
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

        var roster = RosterFor(allocation);
        foreach (var student in roster)
        {
            var rate = ComputeRate(allocation, student.StudentId, student.StudentName);
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

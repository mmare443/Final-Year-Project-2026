using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M11 — Reporting &amp; Analytics. Phase 1 is the executive snapshot;
/// Phase 2 adds enrolment analytics. Counts existing tables only.
/// </summary>
[ApiController]
[Authorize(Policy = "ManagementOnly")]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;

    public ReportsController(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryRecord>> GetSummary(
        CancellationToken cancellationToken)
    {
        var students = await _dbContext.Students
            .AsNoTracking()
            .CountAsync(s => s.EnrolmentStatus == "Enrolled", cancellationToken);
        var staff = await _dbContext.Staff.AsNoTracking().CountAsync(cancellationToken);
        var programmes = await _dbContext.Programmes.AsNoTracking().CountAsync(cancellationToken);
        var faculties = await _dbContext.Faculties.AsNoTracking().CountAsync(cancellationToken);
        var activeAccommodation = await _dbContext.AccommodationRecords
            .AsNoTracking()
            .CountAsync(a => a.Status == HostelController.ActiveStatus, cancellationToken);
        var openWelfareCases = await _dbContext.WelfareCases
            .AsNoTracking()
            .CountAsync(
                c => c.Status == WelfareController.StatusOpen
                    || c.Status == WelfareController.StatusInProgress,
                cancellationToken);

        return Ok(new ReportSummaryRecord
        {
            Students = students,
            Staff = staff,
            Programmes = programmes,
            Faculties = faculties,
            ActiveAccommodation = activeAccommodation,
            OpenWelfareCases = openWelfareCases,
        });
    }

    [HttpGet("enrolment")]
    public async Task<ActionResult<ReportEnrolmentRecord>> GetEnrolment(
        CancellationToken cancellationToken)
    {
        var admissionCounts = await _dbContext.Admissions
            .AsNoTracking()
            .GroupBy(a => new { a.ProgrammeId, a.Status })
            .Select(g => new { g.Key.ProgrammeId, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var enrolledByProgramme = await _dbContext.Students
            .AsNoTracking()
            .Where(s => s.EnrolmentStatus == "Enrolled")
            .GroupBy(s => s.ProgrammeId)
            .Select(g => new { ProgrammeId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var programmes = await _dbContext.Programmes
            .AsNoTracking()
            .Select(p => new
            {
                p.ProgrammeId,
                p.ProgrammeName,
                p.DepartmentId,
                DepartmentName = p.Department.DepartmentName,
                FacultyId = p.Department.FacultyId,
                FacultyName = p.Department.Faculty.FacultyName,
            })
            .OrderBy(p => p.FacultyName)
            .ThenBy(p => p.ProgrammeName)
            .ToListAsync(cancellationToken);

        var enrolledLookup = enrolledByProgramme.ToDictionary(x => x.ProgrammeId, x => x.Count);

        var breakdown = programmes.Select(p =>
        {
            var rows = admissionCounts.Where(c => c.ProgrammeId == p.ProgrammeId).ToList();
            return new EnrolmentProgrammeRecord
            {
                ProgrammeId = p.ProgrammeId,
                ProgrammeName = p.ProgrammeName,
                DepartmentId = p.DepartmentId,
                DepartmentName = p.DepartmentName,
                FacultyId = p.FacultyId,
                FacultyName = p.FacultyName,
                ApplicationsReceived = rows.Sum(c => c.Count),
                ApprovedAdmissions = rows.Where(c => c.Status == "Approved").Sum(c => c.Count),
                RejectedAdmissions = rows.Where(c => c.Status == "Rejected").Sum(c => c.Count),
                EnrolledStudents = enrolledLookup.GetValueOrDefault(p.ProgrammeId),
            };
        }).ToList();

        return Ok(new ReportEnrolmentRecord
        {
            ApplicationsReceived = admissionCounts.Sum(c => c.Count),
            ApprovedAdmissions = admissionCounts.Where(c => c.Status == "Approved").Sum(c => c.Count),
            RejectedAdmissions = admissionCounts.Where(c => c.Status == "Rejected").Sum(c => c.Count),
            EnrolledStudents = enrolledByProgramme.Sum(c => c.Count),
            Programmes = breakdown,
        });
    }
}

public class ReportSummaryRecord
{
    public int Students { get; set; }
    public int Staff { get; set; }
    public int Programmes { get; set; }
    public int Faculties { get; set; }
    public int ActiveAccommodation { get; set; }
    public int OpenWelfareCases { get; set; }
}

public class ReportEnrolmentRecord
{
    public int ApplicationsReceived { get; set; }
    public int ApprovedAdmissions { get; set; }
    public int RejectedAdmissions { get; set; }
    public int EnrolledStudents { get; set; }
    public IReadOnlyList<EnrolmentProgrammeRecord> Programmes { get; set; } =
        Array.Empty<EnrolmentProgrammeRecord>();
}

public class EnrolmentProgrammeRecord
{
    public int ProgrammeId { get; set; }
    public string ProgrammeName { get; set; } = "";
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = "";
    public int ApplicationsReceived { get; set; }
    public int ApprovedAdmissions { get; set; }
    public int RejectedAdmissions { get; set; }
    public int EnrolledStudents { get; set; }
}

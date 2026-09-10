using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M11 Phase 1 — executive snapshot. Counts existing tables only
/// (no new reporting tables). Later phases add filtered analytics
/// and export.
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

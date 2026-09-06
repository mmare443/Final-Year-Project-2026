using LCC_CMS_Api.Services;
using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M7 Phase 5 — JSON academic transcript from published, completed courses.
/// PDF export is a later phase. GPA is the active semester; CGPA is cumulative.
/// </summary>
[ApiController]
[Route("api/students")]
public class TranscriptController : ControllerBase
{
    private readonly CourseResultService _courseResults;
    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public TranscriptController(
        CourseResultService courseResults,
        LccCmsDbContext dbContext,
        ICurrentUser currentUser)
    {
        _courseResults = courseResults;
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [Authorize]
    [HttpGet("{studentNumber}/transcript")]
    public async Task<ActionResult<TranscriptRecord>> GetTranscript(
        string studentNumber,
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken)
            || _currentUser.UserId is not int)
        {
            return Unauthorized();
        }

        var student = await _dbContext.Students
            .AsNoTracking()
            .Include(s => s.Programme)
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber, cancellationToken);
        if (student is null) return NotFound();

        var role = RoleNames.ToPolicyRole(_currentUser.Role);
        var allowed = role.Equals(RoleNames.RegistrarAdmin, StringComparison.OrdinalIgnoreCase)
            || (role.Equals(RoleNames.Student, StringComparison.OrdinalIgnoreCase)
                && _currentUser.StudentId == student.StudentId)
            || (role.Equals(RoleNames.HoD, StringComparison.OrdinalIgnoreCase)
                && await IsHoDOwnerAsync(student.Programme.DepartmentId, cancellationToken));
        if (!allowed) return Forbid();

        var result = await _courseResults.GetTranscriptAsync(studentNumber);
        if (result is null) return NotFound();
        return Ok(result);
    }

    private async Task<bool> IsHoDOwnerAsync(int departmentId, CancellationToken cancellationToken)
    {
        if (_currentUser.StaffId is not int staffId) return false;

        var staffDepartmentId = await _dbContext.Staff
            .AsNoTracking()
            .Where(s => s.StaffId == staffId)
            .Select(s => (int?)s.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

        return staffDepartmentId == departmentId;
    }
}

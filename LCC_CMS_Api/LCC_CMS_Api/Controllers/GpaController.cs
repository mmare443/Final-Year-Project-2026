using LCC_CMS_Api.Services;
using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M7 Phase 4 — GPA and CGPA from published, completed course attempts.
/// Transcript JSON is GET /api/students/{studentNumber}/transcript.
/// </summary>
[ApiController]
[Route("api/students")]
public class GpaController : ControllerBase
{
    private readonly CourseResultService _courseResults;
    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GpaController(
        CourseResultService courseResults,
        LccCmsDbContext dbContext,
        ICurrentUser currentUser)
    {
        _courseResults = courseResults;
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [Authorize]
    [HttpGet("{studentNumber}/gpa")]
    public async Task<ActionResult<GpaRecord>> GetGpa(
        string studentNumber,
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeStudentAsync(studentNumber, cancellationToken);
        if (access.Error is not null) return access.Error;

        var result = await _courseResults.GetGpaAsync(studentNumber, semesterId);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [Authorize]
    [HttpGet("{studentNumber}/cgpa")]
    public async Task<ActionResult<CgpaRecord>> GetCgpa(
        string studentNumber,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeStudentAsync(studentNumber, cancellationToken);
        if (access.Error is not null) return access.Error;

        var result = await _courseResults.GetCgpaAsync(studentNumber);
        if (result is null) return NotFound();
        return Ok(result);
    }

    private async Task<(int? StudentId, ActionResult? Error)> AuthorizeStudentAsync(
        string studentNumber,
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken)
            || _currentUser.UserId is not int)
        {
            return (null, Unauthorized());
        }

        var student = await _dbContext.Students
            .AsNoTracking()
            .Include(s => s.Programme)
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber, cancellationToken);
        if (student is null) return (null, NotFound());

        var role = RoleNames.ToPolicyRole(_currentUser.Role);
        if (role.Equals(RoleNames.RegistrarAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return (student.StudentId, null);
        }

        if (role.Equals(RoleNames.Student, StringComparison.OrdinalIgnoreCase))
        {
            return _currentUser.StudentId == student.StudentId
                ? (student.StudentId, null)
                : (null, Forbid());
        }

        if (role.Equals(RoleNames.HoD, StringComparison.OrdinalIgnoreCase)
            && await IsHoDOwnerAsync(student.Programme.DepartmentId, cancellationToken))
        {
            return (student.StudentId, null);
        }

        return (null, Forbid());
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

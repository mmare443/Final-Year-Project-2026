using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M7 — Assessment &amp; Results Management (Module Specification).
///
/// Phase 1: assessments persist through EF Core. Spec entities:
/// assessments C/R/U — no Delete. Grades, GPA, publication, and
/// transcripts are later phases.
///
/// Weight of all assessments on one allocation must not exceed 100.
/// Course fields come from Allocation.Course.
/// </summary>
[ApiController]
[Route("api/assessments")]
public class AssessmentsController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AssessmentsController(LccCmsDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [Authorize(Policy = "HoDOnly")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssessmentRecord>>> GetAssessments()
    {
        var departmentId = await ResolveHoDDepartmentIdAsync(HttpContext.RequestAborted);
        if (departmentId is null) return Unauthorized();

        var assessments = await AssessmentGraph()
            .AsNoTracking()
            .Where(a => a.Allocation.Staff.DepartmentId == departmentId.Value)
            .OrderBy(a => a.AllocationId)
            .ThenBy(a => a.Title)
            .ToListAsync();

        return Ok(assessments.Select(ToRecord));
    }

    [Authorize(Policy = "HoDOnly")]
    [HttpGet("{id}")]
    public async Task<ActionResult<AssessmentRecord>> GetAssessment(
        int id,
        CancellationToken cancellationToken)
    {
        var departmentId = await ResolveHoDDepartmentIdAsync(cancellationToken);
        if (departmentId is null) return Unauthorized();

        var assessment = await AssessmentGraph()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.AssessmentId == id
                    && a.Allocation.Staff.DepartmentId == departmentId.Value,
                cancellationToken);
        if (assessment is null) return NotFound();
        return Ok(ToRecord(assessment));
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

    [Authorize(Policy = "LecturerOnly")]
    [HttpPost]
    public async Task<ActionResult<AssessmentRecord>> CreateAssessment([FromBody] AssessmentWriteRequest request)
    {
        var error = ValidateWrite(request);
        if (error is not null) return BadRequest(error);

        var allocation = await LoadAllocation(request.AllocationId);
        if (allocation is null) return BadRequest("Course allocation not found.");

        if (await WeightWouldExceedAsync(request.AllocationId, request.WeightPercent, excludeAssessmentId: null))
        {
            return BadRequest("Total assessment weights for this class cannot exceed 100.");
        }

        var assessment = new Assessment
        {
            AllocationId = request.AllocationId,
            Title = request.Title.Trim(),
            WeightPercent = request.WeightPercent,
            MaxMarks = request.MaxMarks,
        };
        _dbContext.Assessments.Add(assessment);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        assessment.Allocation = allocation;
        return Ok(ToRecord(assessment));
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpPut("{id}")]
    public async Task<ActionResult<AssessmentRecord>> UpdateAssessment(int id, [FromBody] AssessmentWriteRequest request)
    {
        var error = ValidateWrite(request);
        if (error is not null) return BadRequest(error);

        var assessment = await AssessmentGraph().FirstOrDefaultAsync(a => a.AssessmentId == id);
        if (assessment is null) return NotFound();

        var allocation = await LoadAllocation(request.AllocationId);
        if (allocation is null) return BadRequest("Course allocation not found.");

        if (await WeightWouldExceedAsync(request.AllocationId, request.WeightPercent, excludeAssessmentId: id))
        {
            return BadRequest("Total assessment weights for this class cannot exceed 100.");
        }

        assessment.AllocationId = request.AllocationId;
        assessment.Title = request.Title.Trim();
        assessment.WeightPercent = request.WeightPercent;
        assessment.MaxMarks = request.MaxMarks;

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        assessment.Allocation = allocation;
        return Ok(ToRecord(assessment));
    }

    private static string? ValidateWrite(AssessmentWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return "Title is required.";
        if (request.Title.Trim().Length > 150) return "Title must be 150 characters or fewer.";
        if (request.WeightPercent <= 0 || request.WeightPercent > 100)
        {
            return "Weight percent must be greater than 0 and at most 100.";
        }
        if (request.MaxMarks <= 0) return "Maximum marks must be greater than 0.";
        return null;
    }

    private async Task<bool> WeightWouldExceedAsync(int allocationId, decimal weight, int? excludeAssessmentId)
    {
        var query = _dbContext.Assessments
            .AsNoTracking()
            .Where(a => a.AllocationId == allocationId);

        if (excludeAssessmentId is not null)
        {
            query = query.Where(a => a.AssessmentId != excludeAssessmentId);
        }

        var current = await query.SumAsync(a => a.WeightPercent);
        return current + weight > 100m;
    }

    private IQueryable<Assessment> AssessmentGraph()
    {
        return _dbContext.Assessments
            .Include(a => a.Allocation)
                .ThenInclude(al => al.Course);
    }

    private async Task<CourseAllocation?> LoadAllocation(int allocationId)
    {
        return await _dbContext.CourseAllocations
            .AsNoTracking()
            .Include(a => a.Course)
            .FirstOrDefaultAsync(a => a.AllocationId == allocationId);
    }

    private static AssessmentRecord ToRecord(Assessment assessment)
    {
        var course = assessment.Allocation.Course;
        return new AssessmentRecord
        {
            Id = assessment.AssessmentId,
            AllocationId = assessment.AllocationId,
            CourseId = course.CourseId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            Title = assessment.Title,
            WeightPercent = assessment.WeightPercent,
            MaxMarks = assessment.MaxMarks,
        };
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the assessment.";

        if (ex.InnerException is not SqlException sql)
        {
            return false;
        }

        if (sql.Number == 547)
        {
            message = "Course allocation was not found.";
            return true;
        }

        return false;
    }
}

public class AssessmentRecord
{
    public int Id { get; set; }
    public int AllocationId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal WeightPercent { get; set; }
    public decimal MaxMarks { get; set; }
}

public class AssessmentWriteRequest
{
    public int AllocationId { get; set; }
    public string Title { get; set; } = "";
    public decimal WeightPercent { get; set; }
    public decimal MaxMarks { get; set; }
}

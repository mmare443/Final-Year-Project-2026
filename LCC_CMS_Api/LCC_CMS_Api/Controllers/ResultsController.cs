using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M7 Phase 3 — student-visible published results.
///
/// Only grades with Published = true are returned. /me uses ICurrentUser
/// (lab: X-User-Id). GPA, CGPA, and transcripts are keyed by student number.
/// </summary>
[ApiController]
[Route("api/results")]
public class ResultsController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ResultsController(LccCmsDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    // [Authorize(Policy = "StudentOnly")] — re-enable once AuthEnabled=true
    [HttpGet("me")]
    public async Task<ActionResult<IEnumerable<PublishedResultRecord>>> GetMyResults(
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken))
        {
            return Unauthorized();
        }

        if (_currentUser.StudentId is not int studentId)
        {
            return NotFound();
        }

        var studentQuery = _dbContext.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId);
        if (!string.IsNullOrEmpty(_currentUser.StudentNumber))
        {
            studentQuery = studentQuery.Where(s => s.StudentNumber == _currentUser.StudentNumber);
        }

        var studentExists = await studentQuery.AnyAsync(cancellationToken);
        if (!studentExists)
        {
            return NotFound();
        }

        var grades = await _dbContext.Grades
            .AsNoTracking()
            .Where(g => g.StudentId == studentId && g.Published)
            .Include(g => g.Assessment)
                .ThenInclude(a => a.Allocation)
                    .ThenInclude(al => al.Course)
            .OrderBy(g => g.Assessment.Allocation.Course.CourseCode)
            .ThenBy(g => g.Assessment.Title)
            .ToListAsync(cancellationToken);

        return Ok(grades.Select(ToRecord));
    }

    private static PublishedResultRecord ToRecord(Grade grade)
    {
        var assessment = grade.Assessment;
        var course = assessment.Allocation.Course;
        return new PublishedResultRecord
        {
            Id = grade.GradeId,
            AssessmentId = assessment.AssessmentId,
            Title = assessment.Title,
            AllocationId = assessment.AllocationId,
            CourseId = course.CourseId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            WeightPercent = assessment.WeightPercent,
            MaxMarks = assessment.MaxMarks,
            MarksObtained = grade.MarksObtained,
            GradeLetter = grade.GradeLetter,
            Published = grade.Published,
        };
    }
}

public class PublishedResultRecord
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public string Title { get; set; } = "";
    public int AllocationId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public decimal WeightPercent { get; set; }
    public decimal MaxMarks { get; set; }
    public decimal MarksObtained { get; set; }
    public string? GradeLetter { get; set; }
    public bool Published { get; set; }
}

using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M7 Phase 3 — student-visible published results.
///
/// Only grades with Published = true are returned. GPA, CGPA, and
/// transcripts are later phases. Until AuthEnabled=true, /me is the
/// first student by student number (same stand-in as StudentsController).
/// </summary>
[ApiController]
[Route("api/results")]
public class ResultsController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;

    public ResultsController(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // [Authorize(Policy = "StudentOnly")] — re-enable once AuthEnabled=true
    [HttpGet("me")]
    public async Task<ActionResult<IEnumerable<PublishedResultRecord>>> GetMyResults()
    {
        var student = await _dbContext.Students
            .AsNoTracking()
            .OrderBy(s => s.StudentNumber)
            .FirstOrDefaultAsync();
        if (student is null) return NotFound();

        var grades = await _dbContext.Grades
            .AsNoTracking()
            .Where(g => g.StudentId == student.StudentId && g.Published)
            .Include(g => g.Assessment)
                .ThenInclude(a => a.Allocation)
                    .ThenInclude(al => al.Course)
            .OrderBy(g => g.Assessment.Allocation.Course.CourseCode)
            .ThenBy(g => g.Assessment.Title)
            .ToListAsync();

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

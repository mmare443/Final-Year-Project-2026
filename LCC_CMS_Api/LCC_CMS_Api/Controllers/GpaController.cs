using LCC_CMS_Api.Services;
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

    public GpaController(CourseResultService courseResults)
    {
        _courseResults = courseResults;
    }

    [HttpGet("{studentNumber}/gpa")]
    public async Task<ActionResult<GpaRecord>> GetGpa(string studentNumber, [FromQuery] int? semesterId)
    {
        var result = await _courseResults.GetGpaAsync(studentNumber, semesterId);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{studentNumber}/cgpa")]
    public async Task<ActionResult<CgpaRecord>> GetCgpa(string studentNumber)
    {
        var result = await _courseResults.GetCgpaAsync(studentNumber);
        if (result is null) return NotFound();
        return Ok(result);
    }
}

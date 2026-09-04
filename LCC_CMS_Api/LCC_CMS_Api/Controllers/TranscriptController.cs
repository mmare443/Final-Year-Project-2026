using LCC_CMS_Api.Services;
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

    public TranscriptController(CourseResultService courseResults)
    {
        _courseResults = courseResults;
    }

    [HttpGet("{studentNumber}/transcript")]
    public async Task<ActionResult<TranscriptRecord>> GetTranscript(string studentNumber)
    {
        var result = await _courseResults.GetTranscriptAsync(studentNumber);
        if (result is null) return NotFound();
        return Ok(result);
    }
}

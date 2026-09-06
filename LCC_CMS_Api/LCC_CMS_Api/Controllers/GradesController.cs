using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M7 Phase 2–3 — mark entry and publication for assessments.
///
/// Roster = M4 Approved registrations for the assessment's allocation.
/// Public student id is StudentNumber; persisted StudentId is the int PK.
/// GradeLetter is derived from assessment percentage using LCC letters
/// A/B/C/D/F. Point values (A=4 … F=0) live on grade_scale for later GPA.
/// SaveGrades always stores Published = false and refuses rows that are
/// already published. PUT publish sets Published = true on every grade
/// for that assessment. Post-publication changes use
/// PUT /api/grades/{id}/override (justification + audit log). Students
/// read published rows only via GET /api/results/me.
/// </summary>
[ApiController]
[Route("api/assessments")]
public class GradesController : ControllerBase
{
    // Percentage bands mapped onto the official A/B/C/D/F letters.
    // SRS v1.2 confirms point values, not cut-offs; these bands are the
    // Phase 0 80/70/60/50 thresholds with P→D and F+/F→F.
    private const decimal BandA = 80m;
    private const decimal BandB = 70m;
    private const decimal BandC = 60m;
    private const decimal BandD = 50m;

    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GradesController(LccCmsDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpGet("{id}/grades")]
    public async Task<ActionResult<IEnumerable<GradeRecord>>> GetGrades(int id)
    {
        var assessment = await _dbContext.Assessments
            .Include(a => a.Allocation)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssessmentId == id);
        if (assessment is null) return NotFound();
        var ownershipError = await RequireLecturerAllocationAsync(assessment.AllocationId, HttpContext.RequestAborted);
        if (ownershipError is not null) return ownershipError;

        return Ok(await LoadRosterGradesAsync(assessment));
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpPut("{id}/grades")]
    public async Task<ActionResult<IEnumerable<GradeRecord>>> SaveGrades(int id, [FromBody] SaveGradesRequest request)
    {
        var assessment = await _dbContext.Assessments
            .Include(a => a.Allocation)
            .FirstOrDefaultAsync(a => a.AssessmentId == id);
        if (assessment is null) return NotFound();
        var ownershipError = await RequireLecturerAllocationAsync(assessment.AllocationId, HttpContext.RequestAborted);
        if (ownershipError is not null) return ownershipError;

        if (request.Grades is null || request.Grades.Count == 0)
        {
            return BadRequest("At least one grade is required.");
        }

        var numbers = request.Grades.Select(g => g.StudentId?.Trim() ?? "").ToList();
        if (numbers.Any(string.IsNullOrWhiteSpace))
        {
            return BadRequest("Student is required for each grade.");
        }

        if (numbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != numbers.Count)
        {
            return BadRequest("Each student may appear only once.");
        }

        foreach (var entry in request.Grades)
        {
            if (entry.MarksObtained < 0 || entry.MarksObtained > assessment.MaxMarks)
            {
                return BadRequest($"Marks must be between 0 and {assessment.MaxMarks}.");
            }
        }

        var students = await _dbContext.Students
            .Where(s => numbers.Contains(s.StudentNumber))
            .ToListAsync();
        if (students.Count != numbers.Count)
        {
            return BadRequest("One or more students were not found.");
        }

        var byNumber = students.ToDictionary(s => s.StudentNumber, StringComparer.OrdinalIgnoreCase);
        var rosterIds = await ApprovedRosterStudentIdsAsync(assessment.AllocationId);

        foreach (var entry in request.Grades)
        {
            var student = byNumber[entry.StudentId.Trim()];
            if (!rosterIds.Contains(student.StudentId))
            {
                return BadRequest("One or more students are not on the approved class roster.");
            }
        }

        foreach (var entry in request.Grades)
        {
            var student = byNumber[entry.StudentId.Trim()];
            var existing = await _dbContext.Grades
                .FirstOrDefaultAsync(g => g.AssessmentId == id && g.StudentId == student.StudentId);
            if (existing is not null && existing.Published)
            {
                return Conflict("Published grades can only be changed via override.");
            }
        }

        foreach (var entry in request.Grades)
        {
            var student = byNumber[entry.StudentId.Trim()];
            var letter = LetterFromMarks(entry.MarksObtained, assessment.MaxMarks);
            var existing = await _dbContext.Grades
                .FirstOrDefaultAsync(g => g.AssessmentId == id && g.StudentId == student.StudentId);

            if (existing is null)
            {
                _dbContext.Grades.Add(new Grade
                {
                    AssessmentId = id,
                    StudentId = student.StudentId,
                    MarksObtained = entry.MarksObtained,
                    GradeLetter = letter,
                    Published = false,
                });
            }
            else
            {
                existing.MarksObtained = entry.MarksObtained;
                existing.GradeLetter = letter;
                existing.Published = false;
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(await LoadRosterGradesAsync(assessment));
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPut("{id}/publish")]
    public async Task<ActionResult<IEnumerable<GradeRecord>>> PublishGrades(int id)
    {
        var assessment = await _dbContext.Assessments
            .FirstOrDefaultAsync(a => a.AssessmentId == id);
        if (assessment is null) return NotFound();

        var grades = await _dbContext.Grades
            .Where(g => g.AssessmentId == id)
            .ToListAsync();

        foreach (var grade in grades)
        {
            grade.Published = true;
        }

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(await LoadRosterGradesAsync(assessment));
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPut("~/api/grades/{id}/override")]
    public async Task<ActionResult<GradeRecord>> OverrideGrade(
        int id,
        [FromBody] OverrideGradeRequest request,
        CancellationToken cancellationToken)
    {
        var justification = request.Justification?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(justification))
        {
            return BadRequest("A justification is required to override a published grade.");
        }
        if (justification.Length > 500)
        {
            return BadRequest("Justification cannot exceed 500 characters.");
        }

        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is not int userId)
        {
            return Unauthorized();
        }
        if (_currentUser.StaffId is not int staffId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var grade = await _dbContext.Grades
            .Include(g => g.Assessment)
            .Include(g => g.Student)
                .ThenInclude(s => s.Admission)
            .FirstOrDefaultAsync(g => g.GradeId == id);
        if (grade is null) return NotFound();

        if (!grade.Published)
        {
            return Conflict("Only published grades may be overridden.");
        }

        var maxMarks = grade.Assessment.MaxMarks;
        if (request.MarksObtained < 0 || request.MarksObtained > maxMarks)
        {
            return BadRequest($"Marks must be between 0 and {maxMarks}.");
        }

        var oldLetter = grade.GradeLetter;
        var oldMarks = grade.MarksObtained;
        var letter = LetterFromMarks(request.MarksObtained, maxMarks);

        grade.MarksObtained = request.MarksObtained;
        grade.GradeLetter = letter;
        grade.Published = true;
        grade.OverriddenBy = staffId;
        grade.OverrideJustification = justification;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "Update",
            TableName = "grades",
            RecordId = grade.GradeId.ToString(),
            OldValue = $"marks={oldMarks};letter={oldLetter}",
            NewValue = $"marks={request.MarksObtained};letter={letter};justification={justification}",
            Timestamp = DateTime.UtcNow,
        });

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(ToRecord(grade, grade.Assessment));
    }

    private async Task<List<GradeRecord>> LoadRosterGradesAsync(Assessment assessment)
    {
        var roster = await _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.AllocationId == assessment.AllocationId && r.Status == "Approved")
            .Select(r => new
            {
                r.Student.StudentId,
                r.Student.StudentNumber,
                StudentName = r.Student.Admission != null
                    ? r.Student.Admission.ApplicantName
                    : r.Student.StudentNumber,
            })
            .ToListAsync();

        roster = roster
            .DistinctBy(s => s.StudentId)
            .OrderBy(s => s.StudentName)
            .ToList();

        var grades = await _dbContext.Grades
            .AsNoTracking()
            .Where(g => g.AssessmentId == assessment.AssessmentId)
            .ToListAsync();
        var byStudent = grades.ToDictionary(g => g.StudentId);

        return roster.Select(s =>
        {
            byStudent.TryGetValue(s.StudentId, out var grade);
            return new GradeRecord
            {
                Id = grade?.GradeId ?? 0,
                AssessmentId = assessment.AssessmentId,
                StudentId = s.StudentNumber,
                StudentName = s.StudentName,
                MarksObtained = grade?.MarksObtained,
                GradeLetter = grade?.GradeLetter,
                Published = grade?.Published ?? false,
                OverriddenBy = grade?.OverriddenBy,
                OverrideJustification = grade?.OverrideJustification,
            };
        }).ToList();
    }

    private static GradeRecord ToRecord(Grade grade, Assessment assessment)
    {
        return new GradeRecord
        {
            Id = grade.GradeId,
            AssessmentId = assessment.AssessmentId,
            StudentId = grade.Student.StudentNumber,
            StudentName = grade.Student.Admission?.ApplicantName ?? grade.Student.StudentNumber,
            MarksObtained = grade.MarksObtained,
            GradeLetter = grade.GradeLetter,
            Published = grade.Published,
            OverriddenBy = grade.OverriddenBy,
            OverrideJustification = grade.OverrideJustification,
        };
    }

    private async Task<HashSet<int>> ApprovedRosterStudentIdsAsync(int allocationId)
    {
        var ids = await _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.AllocationId == allocationId && r.Status == "Approved")
            .Select(r => r.StudentId)
            .ToListAsync();

        return ids.ToHashSet();
    }

    private async Task<ActionResult?> RequireLecturerAllocationAsync(
        int allocationId,
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken)
            || _currentUser.StaffId is not int staffId)
        {
            return Unauthorized();
        }

        var ownsAllocation = await _dbContext.CourseAllocations
            .AsNoTracking()
            .AnyAsync(a => a.AllocationId == allocationId && a.StaffId == staffId, cancellationToken);
        return ownsAllocation ? null : Forbid();
    }

    internal static string LetterFromMarks(decimal marksObtained, decimal maxMarks)
    {
        var percent = maxMarks <= 0 ? 0m : 100m * marksObtained / maxMarks;
        if (percent >= BandA) return "A";
        if (percent >= BandB) return "B";
        if (percent >= BandC) return "C";
        if (percent >= BandD) return "D";
        return "F";
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the grades.";

        if (ex.InnerException is not SqlException sql)
        {
            return false;
        }

        if (sql.Number is 2601 or 2627)
        {
            status = StatusCodes.Status409Conflict;
            message = "A grade already exists for this student and assessment.";
            return true;
        }

        if (sql.Number == 547)
        {
            message = "A related record was not found (assessment or student).";
            return true;
        }

        return false;
    }
}

public class GradeRecord
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public decimal? MarksObtained { get; set; }
    public string? GradeLetter { get; set; }
    public bool Published { get; set; }
    public int? OverriddenBy { get; set; }
    public string? OverrideJustification { get; set; }
}

public class OverrideGradeRequest
{
    public decimal MarksObtained { get; set; }
    public string Justification { get; set; } = "";
}

public class SaveGradesRequest
{
    public List<GradeMarkEntry> Grades { get; set; } = new();
}

public class GradeMarkEntry
{
    public string StudentId { get; set; } = "";
    public decimal MarksObtained { get; set; }
}

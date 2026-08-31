using Microsoft.AspNetCore.Mvc;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M6 — Learning &amp; Assignment Management (Module Specification).
///
/// SKELETON: in-memory, same pattern as M3–M5. Files land on
/// wwwroot/uploads/learning (same local-disk stand-in as M1 admissions
/// docs) — swap the Write path for Azure Blob Storage when cloud access
/// exists; store the blob URL in Path / FileUrl. Spec entities:
/// assignments C/R/U/D, submissions C/R/U, courses R. Learning-material
/// metadata is not its own schema table; it is kept here so lecturers can
/// still distribute files before a dedicated materials table exists.
///
/// Late-work rule: submissions after dueDate are rejected unless the
/// lecturer has set AllowLateSubmissions on that assignment. Accepted
/// late work is flagged IsLate = true. Grading is 0..MaxMarks with
/// written feedback.
///
/// Roster of who may submit = M4 Approved registrations for the
/// allocation's course/semester.
/// </summary>
[ApiController]
[Route("api/learning")]
public class LearningController : ControllerBase
{
    private static readonly string[] AllowedExtensions =
        { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".zip", ".jpg", ".jpeg", ".png", ".txt" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly List<LearningMaterialRecord> _materials = new();
    private static readonly List<AssignmentRecord> _assignments = new();
    private static readonly List<SubmissionRecord> _submissions = new();
    private static int _nextMaterialId = 1;
    private static int _nextAssignmentId = 1;
    private static int _nextSubmissionId = 1;
    private static bool _seeded;

    private readonly IWebHostEnvironment _env;

    public LearningController(IWebHostEnvironment env)
    {
        _env = env;
        EnsureSeed();
    }

    private static void EnsureSeed()
    {
        if (_seeded) return;
        _seeded = true;

        _assignments.Add(BuildAssignment(1, "Case Study 1",
            "Write a 1,500-word case analysis of a PNG agribusiness. Submit as PDF.",
            "2026-12-01T23:59:00Z", 100, allowLate: false));
        _assignments.Add(BuildAssignment(1, "Week 2 reading notes",
            "One-page notes on BAM101 lecture 2. Due early in semester — used to demo the late-submission rule.",
            "2026-03-01T23:59:00Z", 20, allowLate: false));
    }

    private static AssignmentRecord BuildAssignment(
        int allocationId, string title, string instructions, string dueDate, decimal maxMarks, bool allowLate)
    {
        var allocation = AcademicStructureController._courseAllocations.First(a => a.Id == allocationId);
        var course = AcademicStructureController._courses.First(c => c.Id == allocation.CourseId);
        return new AssignmentRecord
        {
            Id = _nextAssignmentId++,
            AllocationId = allocationId,
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            Title = title,
            Instructions = instructions,
            DueDate = dueDate,
            MaxMarks = maxMarks,
            AllowLateSubmissions = allowLate,
        };
    }

    // --- Materials ---

    [HttpGet("materials")]
    public ActionResult<IEnumerable<LearningMaterialRecord>> GetMaterials(
        [FromQuery] int? allocationId,
        [FromQuery] string? studentId)
    {
        IEnumerable<LearningMaterialRecord> results = _materials;
        if (allocationId is not null)
        {
            results = results.Where(m => m.AllocationId == allocationId);
        }
        else if (studentId is not null)
        {
            var allowed = AllocationIdsForStudent(studentId);
            results = results.Where(m => allowed.Contains(m.AllocationId));
        }
        return Ok(results.OrderByDescending(m => m.UploadedAt));
    }

    [HttpPost("materials")]
    [RequestSizeLimit(MaxFileSizeBytes + 1024)]
    public ActionResult<LearningMaterialRecord> UploadMaterial(
        [FromForm] int allocationId,
        [FromForm] string title,
        IFormFile? file)
    {
        var allocation = AcademicStructureController._courseAllocations.FirstOrDefault(a => a.Id == allocationId);
        if (allocation is null) return BadRequest("Course allocation not found.");
        if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required.");

        var saved = SaveFile(file, "materials");
        if (saved.Error is not null) return BadRequest(saved.Error);

        var course = AcademicStructureController._courses.First(c => c.Id == allocation.CourseId);
        var material = new LearningMaterialRecord
        {
            Id = _nextMaterialId++,
            AllocationId = allocationId,
            CourseCode = course.Code,
            CourseName = course.Name,
            Title = title.Trim(),
            Path = saved.Path!,
            FileName = saved.OriginalName!,
            UploadedAt = DateTime.UtcNow,
        };
        _materials.Add(material);
        return Ok(material);
    }

    [HttpDelete("materials/{id}")]
    public IActionResult DeleteMaterial(int id)
    {
        var material = _materials.FirstOrDefault(m => m.Id == id);
        if (material is null) return NotFound();
        _materials.Remove(material);
        return Ok(material);
    }

    // --- Assignments ---

    [HttpGet("assignments")]
    public ActionResult<IEnumerable<AssignmentRecord>> GetAssignments(
        [FromQuery] int? allocationId,
        [FromQuery] string? studentId)
    {
        IEnumerable<AssignmentRecord> results = _assignments;
        if (allocationId is not null)
        {
            results = results.Where(a => a.AllocationId == allocationId);
        }
        else if (studentId is not null)
        {
            var allowed = AllocationIdsForStudent(studentId);
            results = results.Where(a => allowed.Contains(a.AllocationId));
        }
        return Ok(results.OrderBy(a => a.DueDate));
    }

    [HttpPost("assignments")]
    public ActionResult<AssignmentRecord> CreateAssignment([FromBody] AssignmentWriteRequest request)
    {
        var allocation = AcademicStructureController._courseAllocations
            .FirstOrDefault(a => a.Id == request.AllocationId);
        if (allocation is null) return BadRequest("Course allocation not found.");
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest("Title is required.");
        if (request.MaxMarks <= 0) return BadRequest("Maximum marks must be greater than 0.");
        if (string.IsNullOrWhiteSpace(request.DueDate)) return BadRequest("Due date is required.");

        var course = AcademicStructureController._courses.First(c => c.Id == allocation.CourseId);
        var created = new AssignmentRecord
        {
            Id = _nextAssignmentId++,
            AllocationId = request.AllocationId,
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            Title = request.Title.Trim(),
            Instructions = request.Instructions?.Trim() ?? "",
            DueDate = request.DueDate,
            MaxMarks = request.MaxMarks,
            AllowLateSubmissions = request.AllowLateSubmissions,
        };
        _assignments.Add(created);
        return Ok(created);
    }

    [HttpPut("assignments/{id}")]
    public ActionResult<AssignmentRecord> UpdateAssignment(int id, [FromBody] AssignmentWriteRequest request)
    {
        var assignment = _assignments.FirstOrDefault(a => a.Id == id);
        if (assignment is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest("Title is required.");
        if (request.MaxMarks <= 0) return BadRequest("Maximum marks must be greater than 0.");

        assignment.Title = request.Title.Trim();
        assignment.Instructions = request.Instructions?.Trim() ?? "";
        assignment.DueDate = string.IsNullOrWhiteSpace(request.DueDate) ? assignment.DueDate : request.DueDate;
        assignment.MaxMarks = request.MaxMarks;
        assignment.AllowLateSubmissions = request.AllowLateSubmissions;
        return Ok(assignment);
    }

    [HttpDelete("assignments/{id}")]
    public IActionResult DeleteAssignment(int id)
    {
        var assignment = _assignments.FirstOrDefault(a => a.Id == id);
        if (assignment is null) return NotFound();
        _submissions.RemoveAll(s => s.AssignmentId == id);
        _assignments.Remove(assignment);
        return Ok(assignment);
    }

    // --- Submissions ---

    [HttpGet("assignments/{id}/submissions")]
    public ActionResult<IEnumerable<SubmissionRecord>> GetSubmissions(int id)
    {
        if (_assignments.All(a => a.Id != id)) return NotFound();
        return Ok(_submissions.Where(s => s.AssignmentId == id).OrderBy(s => s.StudentName));
    }

    [HttpGet("submissions")]
    public ActionResult<IEnumerable<SubmissionRecord>> GetMySubmissions([FromQuery] string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId)) return BadRequest("studentId is required.");
        return Ok(_submissions.Where(s => s.StudentId == studentId));
    }

    [HttpPost("assignments/{id}/submissions")]
    [RequestSizeLimit(MaxFileSizeBytes + 1024)]
    public ActionResult<SubmissionRecord> Submit(
        int id,
        [FromForm] string studentId,
        IFormFile? file)
    {
        var assignment = _assignments.FirstOrDefault(a => a.Id == id);
        if (assignment is null) return NotFound();
        if (string.IsNullOrWhiteSpace(studentId)) return BadRequest("studentId is required.");

        var enrolled = RosterFor(assignment.AllocationId).Any(s => s.StudentId == studentId);
        if (!enrolled)
        {
            return BadRequest("You are not registered for this unit.");
        }

        var due = DateTime.Parse(assignment.DueDate, null, System.Globalization.DateTimeStyles.RoundtripKind);
        if (due.Kind == DateTimeKind.Unspecified) due = DateTime.SpecifyKind(due, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        var isLate = now > due.ToUniversalTime();
        if (isLate && !assignment.AllowLateSubmissions)
        {
            return BadRequest(
                "This assignment is past its due date. Submissions are restricted until the lecturer overrides (allow late submissions).");
        }

        var saved = SaveFile(file, "submissions");
        if (saved.Error is not null) return BadRequest(saved.Error);

        var student = StudentsController._students.FirstOrDefault(s => s.Id == studentId);
        var existing = _submissions.FirstOrDefault(s => s.AssignmentId == id && s.StudentId == studentId);
        if (existing is null)
        {
            var created = new SubmissionRecord
            {
                Id = _nextSubmissionId++,
                AssignmentId = id,
                StudentId = studentId,
                StudentName = student?.FullName ?? studentId,
                FileUrl = saved.Path!,
                FileName = saved.OriginalName!,
                SubmittedAt = now,
                IsLate = isLate,
                MarksAwarded = null,
                Feedback = null,
            };
            _submissions.Add(created);
            return Ok(created);
        }

        existing.FileUrl = saved.Path!;
        existing.FileName = saved.OriginalName!;
        existing.SubmittedAt = now;
        existing.IsLate = isLate;
        existing.MarksAwarded = null;
        existing.Feedback = null;
        return Ok(existing);
    }

    [HttpPut("submissions/{id}/grade")]
    public ActionResult<SubmissionRecord> Grade(int id, [FromBody] GradeRequest request)
    {
        var submission = _submissions.FirstOrDefault(s => s.Id == id);
        if (submission is null) return NotFound();

        var assignment = _assignments.First(a => a.Id == submission.AssignmentId);
        if (request.MarksAwarded < 0 || request.MarksAwarded > assignment.MaxMarks)
        {
            return BadRequest($"Marks must be between 0 and {assignment.MaxMarks}.");
        }

        submission.MarksAwarded = request.MarksAwarded;
        submission.Feedback = request.Feedback?.Trim() ?? "";
        return Ok(submission);
    }

    [HttpGet("summary")]
    public ActionResult<object> GetSummary([FromQuery] string? studentId)
    {
        if (studentId is not null)
        {
            var allowed = AllocationIdsForStudent(studentId);
            var mine = _assignments.Where(a => allowed.Contains(a.AllocationId)).ToList();
            var submitted = _submissions.Count(s => s.StudentId == studentId);
            var pending = mine.Count(a => !_submissions.Any(s => s.AssignmentId == a.Id && s.StudentId == studentId));
            return Ok(new { assignmentCount = mine.Count, submittedCount = submitted, pendingCount = pending });
        }

        var ungraded = _submissions.Count(s => s.MarksAwarded is null);
        return Ok(new
        {
            assignmentCount = _assignments.Count,
            submissionCount = _submissions.Count,
            pendingGradingCount = ungraded,
        });
    }

    private SavedFile SaveFile(IFormFile? file, string subfolder)
    {
        if (file is null) return new SavedFile { Error = "No file received." };

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            return new SavedFile { Error = $"File type '{ext}' not allowed." };
        }
        if (file.Length > MaxFileSizeBytes)
        {
            return new SavedFile { Error = "File is too large. Maximum size is 10 MB." };
        }

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "learning", subfolder);
        Directory.CreateDirectory(uploadsFolder);
        var safeFileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadsFolder, safeFileName);
        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            file.CopyTo(stream);
        }

        return new SavedFile
        {
            Path = $"/uploads/learning/{subfolder}/{safeFileName}",
            OriginalName = file.FileName,
        };
    }

    private static HashSet<int> AllocationIdsForStudent(string studentId)
    {
        var courseSemester = RegistrationsController._registrations
            .Where(r => r.StudentId == studentId && r.Status == "Approved")
            .Select(r => (r.CourseId, r.SemesterId))
            .ToHashSet();

        return AcademicStructureController._courseAllocations
            .Where(a => courseSemester.Contains((a.CourseId, a.SemesterId)))
            .Select(a => a.Id)
            .ToHashSet();
    }

    private static List<AttendanceRosterStudent> RosterFor(int allocationId)
    {
        var allocation = AcademicStructureController._courseAllocations.FirstOrDefault(a => a.Id == allocationId);
        if (allocation is null) return new List<AttendanceRosterStudent>();

        return RegistrationsController._registrations
            .Where(r => r.CourseId == allocation.CourseId &&
                        r.SemesterId == allocation.SemesterId &&
                        r.Status == "Approved")
            .Select(r => new AttendanceRosterStudent { StudentId = r.StudentId, StudentName = r.StudentName })
            .DistinctBy(s => s.StudentId)
            .ToList();
    }

    private class SavedFile
    {
        public string? Path { get; set; }
        public string? OriginalName { get; set; }
        public string? Error { get; set; }
    }
}

public class LearningMaterialRecord
{
    public int Id { get; set; }
    public int AllocationId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime UploadedAt { get; set; }
}

public class AssignmentRecord
{
    public int Id { get; set; }
    public int AllocationId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string DueDate { get; set; } = "";
    public decimal MaxMarks { get; set; }
    public bool AllowLateSubmissions { get; set; }
}

public class AssignmentWriteRequest
{
    public int AllocationId { get; set; }
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    public string DueDate { get; set; } = "";
    public decimal MaxMarks { get; set; }
    public bool AllowLateSubmissions { get; set; }
}

public class SubmissionRecord
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string FileUrl { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public bool IsLate { get; set; }
    public decimal? MarksAwarded { get; set; }
    public string? Feedback { get; set; }
}

public class GradeRequest
{
    public decimal MarksAwarded { get; set; }
    public string? Feedback { get; set; }
}

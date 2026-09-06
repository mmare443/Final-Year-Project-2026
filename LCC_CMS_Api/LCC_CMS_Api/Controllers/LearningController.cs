using System.Globalization;
using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M6 — Learning &amp; Assignment Management (Module Specification).
///
/// Assignments and submissions persist through EF Core. Materials remain
/// in-memory. Files land on wwwroot/uploads/learning (same local-disk
/// stand-in as M1 admissions docs) — swap the Write path for Azure Blob
/// Storage when cloud access exists; store the blob URL in Path / FileUrl.
/// Course allocations are read through EF Core. Spec entities: assignments
/// C/R/U/D, submissions C/R/U, courses R. Learning-material metadata is
/// not its own schema table; it is kept here so lecturers can still
/// distribute files before a dedicated materials table exists.
///
/// Late-work rule: submissions after dueDate are rejected unless the
/// lecturer has set AllowLateSubmissions on that assignment. That flag is
/// kept on the DTO (no database column). Accepted late work is flagged
/// IsLate = true. Grading is 0..MaxMarks with written feedback.
///
/// Roster of who may submit = M4 Approved registrations for the
/// allocation (EF Core).
/// </summary>
[ApiController]
[Route("api/learning")]
public class LearningController : ControllerBase
{
    private static readonly string[] AllowedExtensions =
        { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".zip", ".jpg", ".jpeg", ".png", ".txt" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly LccCmsDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;

    public LearningController(
        LccCmsDbContext dbContext,
        IFileStorage fileStorage,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
    }

    // --- Materials ---

    [HttpGet("materials")]
    public async Task<ActionResult<IEnumerable<LearningMaterialRecord>>> GetMaterials(
        [FromQuery] int? allocationId,
        [FromQuery] string? studentId)
    {
        var query = _dbContext.LearningMaterials
            .AsNoTracking()
            .Include(m => m.Allocation)
                .ThenInclude(a => a.Course)
            .AsQueryable();
        if (allocationId is not null)
        {
            query = query.Where(m => m.AllocationId == allocationId);
        }
        else if (studentId is not null)
        {
            var allowed = await AllocationIdsForStudent(studentId);
            query = query.Where(m => allowed.Contains(m.AllocationId));
        }

        var materials = await query
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();
        return Ok(materials.Select(ToMaterialRecord));
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpPost("materials")]
    [RequestSizeLimit(MaxFileSizeBytes + 1024)]
    public async Task<ActionResult<LearningMaterialRecord>> UploadMaterial(
        [FromForm] int allocationId,
        [FromForm] string title,
        IFormFile? file)
    {
        var allocation = await LoadAllocation(allocationId);
        if (allocation is null) return BadRequest("Course allocation not found.");
        if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required.");

        var saved = await SaveFileAsync(file, "materials", HttpContext.RequestAborted);
        if (saved.Error is not null) return BadRequest(saved.Error);

        int? uploadedByStaffId = null;
        if (await _currentUser.ResolveAsync(HttpContext.RequestAborted))
        {
            uploadedByStaffId = _currentUser.StaffId;
        }

        var material = new LearningMaterial
        {
            AllocationId = allocationId,
            Title = title.Trim(),
            StorageKey = saved.Path!,
            OriginalFileName = saved.OriginalName!,
            ContentType = saved.ContentType,
            FileSize = saved.Length,
            UploadedAt = DateTime.UtcNow,
            UploadedByStaffId = uploadedByStaffId,
        };
        _dbContext.LearningMaterials.Add(material);
        await _dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        material.Allocation = allocation;
        return Ok(ToMaterialRecord(material));
    }

    [HttpGet("materials/{materialId}/download")]
    public async Task<IActionResult> DownloadMaterial(
        int materialId,
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken)
            || _currentUser.UserId is not int)
        {
            return Unauthorized();
        }

        var material = await _dbContext.LearningMaterials
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.LearningMaterialId == materialId, cancellationToken);
        if (material is null) return NotFound();

        var allocation = await _dbContext.CourseAllocations
            .AsNoTracking()
            .Include(a => a.Staff)
            .FirstOrDefaultAsync(
                a => a.AllocationId == material.AllocationId,
                cancellationToken);
        if (allocation is null) return NotFound();

        var role = RoleNames.ToPolicyRole(_currentUser.Role);
        var allowed = role.Equals(RoleNames.RegistrarAdmin, StringComparison.OrdinalIgnoreCase);

        if (role.Equals(RoleNames.Student, StringComparison.OrdinalIgnoreCase))
        {
            allowed = _currentUser.StudentId is int studentId
                && await _dbContext.Registrations
                    .AsNoTracking()
                    .AnyAsync(
                        r => r.StudentId == studentId
                            && r.AllocationId == material.AllocationId
                            && r.Status == "Approved",
                        cancellationToken);
        }
        else if (role.Equals(RoleNames.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            allowed = _currentUser.StaffId is int staffId
                && allocation.StaffId == staffId;
        }
        else if (role.Equals(RoleNames.HoD, StringComparison.OrdinalIgnoreCase))
        {
            if (_currentUser.StaffId is int staffId)
            {
                var departmentId = await _dbContext.Staff
                    .AsNoTracking()
                    .Where(s => s.StaffId == staffId)
                    .Select(s => (int?)s.DepartmentId)
                    .FirstOrDefaultAsync(cancellationToken);

                var lecturerDepartmentId = await _dbContext.Staff
                    .AsNoTracking()
                    .Where(s => s.StaffId == allocation.StaffId)
                    .Select(s => (int?)s.DepartmentId)
                    .FirstOrDefaultAsync(cancellationToken);

                allowed = departmentId is int currentDepartmentId
                    && lecturerDepartmentId is int allocationDepartmentId
                    && currentDepartmentId == allocationDepartmentId;
            }
        }

        if (!allowed) return Forbid();

        Stream content;
        try
        {
            content = await _fileStorage.OpenReadAsync(material.StorageKey, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound();
        }

        return File(
            content,
            string.IsNullOrWhiteSpace(material.ContentType)
                ? "application/octet-stream"
                : material.ContentType,
            string.IsNullOrWhiteSpace(material.OriginalFileName)
                ? Path.GetFileName(material.StorageKey)
                : material.OriginalFileName,
            enableRangeProcessing: true);
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpDelete("materials/{id}")]
    public async Task<IActionResult> DeleteMaterial(int id, CancellationToken cancellationToken)
    {
        var material = await _dbContext.LearningMaterials
            .Include(m => m.Allocation)
                .ThenInclude(a => a.Course)
            .FirstOrDefaultAsync(m => m.LearningMaterialId == id, cancellationToken);
        if (material is null) return NotFound();

        var dto = ToMaterialRecord(material);
        _dbContext.LearningMaterials.Remove(material);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(dto);
    }

    // --- Assignments ---

    [HttpGet("assignments")]
    public async Task<ActionResult<IEnumerable<AssignmentRecord>>> GetAssignments(
        [FromQuery] int? allocationId,
        [FromQuery] string? studentId)
    {
        var query = AssignmentGraph().AsNoTracking();
        if (allocationId is not null)
        {
            query = query.Where(a => a.AllocationId == allocationId);
        }
        else if (studentId is not null)
        {
            var allowed = await AllocationIdsForStudent(studentId);
            query = query.Where(a => allowed.Contains(a.AllocationId));
        }

        var assignments = await query
            .OrderBy(a => a.DueDate)
            .ToListAsync();

        return Ok(assignments.Select(ToAssignmentRecord));
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpPost("assignments")]
    public async Task<ActionResult<AssignmentRecord>> CreateAssignment([FromBody] AssignmentWriteRequest request)
    {
        var allocation = await LoadAllocation(request.AllocationId);
        if (allocation is null) return BadRequest("Course allocation not found.");
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest("Title is required.");
        if (request.MaxMarks <= 0) return BadRequest("Maximum marks must be greater than 0.");
        if (!TryParseDueDate(request.DueDate, out var dueDate))
        {
            return BadRequest("Due date is required and must be a valid date.");
        }

        var assignment = new Assignment
        {
            AllocationId = request.AllocationId,
            Title = request.Title.Trim(),
            Instructions = request.Instructions?.Trim() ?? "",
            DueDate = dueDate,
            MaxMarks = request.MaxMarks,
            AllowLateSubmissions = request.AllowLateSubmissions,
        };
        _dbContext.Assignments.Add(assignment);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        assignment.Allocation = allocation;
        return Ok(ToAssignmentRecord(assignment));
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpPut("assignments/{id}")]
    public async Task<ActionResult<AssignmentRecord>> UpdateAssignment(int id, [FromBody] AssignmentWriteRequest request)
    {
        var assignment = await AssignmentGraph().FirstOrDefaultAsync(a => a.AssignmentId == id);
        if (assignment is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest("Title is required.");
        if (request.MaxMarks <= 0) return BadRequest("Maximum marks must be greater than 0.");

        assignment.Title = request.Title.Trim();
        assignment.Instructions = request.Instructions?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(request.DueDate))
        {
            if (!TryParseDueDate(request.DueDate, out var dueDate))
            {
                return BadRequest("Due date must be a valid date.");
            }
            assignment.DueDate = dueDate;
        }
        assignment.MaxMarks = request.MaxMarks;
        assignment.AllowLateSubmissions = request.AllowLateSubmissions;

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(ToAssignmentRecord(assignment));
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpDelete("assignments/{id}")]
    public async Task<IActionResult> DeleteAssignment(int id)
    {
        var assignment = await AssignmentGraph().FirstOrDefaultAsync(a => a.AssignmentId == id);
        if (assignment is null) return NotFound();

        var dto = ToAssignmentRecord(assignment);

        var related = await _dbContext.Submissions
            .Where(s => s.AssignmentId == id)
            .ToListAsync();
        _dbContext.Submissions.RemoveRange(related);
        _dbContext.Assignments.Remove(assignment);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(dto);
    }

    // --- Submissions ---

    [HttpGet("assignments/{id}/submissions")]
    public async Task<ActionResult<IEnumerable<SubmissionRecord>>> GetSubmissions(int id)
    {
        var exists = await _dbContext.Assignments.AsNoTracking().AnyAsync(a => a.AssignmentId == id);
        if (!exists) return NotFound();

        var submissions = await SubmissionGraph()
            .AsNoTracking()
            .Where(s => s.AssignmentId == id)
            .ToListAsync();

        return Ok(submissions
            .Select(ToSubmissionRecord)
            .OrderBy(s => s.StudentName));
    }

    [HttpGet("submissions")]
    public async Task<ActionResult<IEnumerable<SubmissionRecord>>> GetMySubmissions([FromQuery] string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId)) return BadRequest("studentId is required.");

        var submissions = await SubmissionGraph()
            .AsNoTracking()
            .Where(s => s.Student.StudentNumber == studentId)
            .ToListAsync();

        return Ok(submissions.Select(ToSubmissionRecord));
    }

    [HttpPost("assignments/{id}/submissions")]
    [RequestSizeLimit(MaxFileSizeBytes + 1024)]
    public async Task<ActionResult<SubmissionRecord>> Submit(
        int id,
        [FromForm] string studentId,
        IFormFile? file)
    {
        var assignment = await _dbContext.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssignmentId == id);
        if (assignment is null) return NotFound();
        if (string.IsNullOrWhiteSpace(studentId)) return BadRequest("studentId is required.");

        var student = await FindStudentByNumberAsync(studentId.Trim());
        if (student is null) return BadRequest("Student was not found.");

        var enrolled = (await RosterFor(assignment.AllocationId)).Any(s => s.StudentId == student.StudentNumber);
        if (!enrolled)
        {
            return BadRequest("You are not registered for this unit.");
        }

        var due = assignment.DueDate;
        if (due.Kind == DateTimeKind.Unspecified) due = DateTime.SpecifyKind(due, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        var isLate = now > due.ToUniversalTime();
        var allowLate = assignment.AllowLateSubmissions;
        if (isLate && !allowLate)
        {
            return BadRequest(
                "This assignment is past its due date. Submissions are restricted until the lecturer overrides (allow late submissions).");
        }

        var saved = await SaveFileAsync(file, "submissions", HttpContext.RequestAborted);
        if (saved.Error is not null) return BadRequest(saved.Error);

        var existing = await _dbContext.Submissions
            .Include(s => s.Student)
                .ThenInclude(st => st.Admission)
            .FirstOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == student.StudentId);

        if (existing is null)
        {
            existing = new Submission
            {
                AssignmentId = id,
                StudentId = student.StudentId,
                FileUrl = saved.Path!,
                OriginalFileName = saved.OriginalName!,
                ContentType = file?.ContentType,
                SubmittedAt = now,
                IsLate = isLate,
                MarksAwarded = null,
                Feedback = null,
            };
            _dbContext.Submissions.Add(existing);
        }
        else
        {
            existing.FileUrl = saved.Path!;
            existing.OriginalFileName = saved.OriginalName!;
            existing.ContentType = file?.ContentType;
            existing.SubmittedAt = now;
            existing.IsLate = isLate;
            existing.MarksAwarded = null;
            existing.Feedback = null;
        }

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        existing.Student = student;
        return Ok(ToSubmissionRecord(existing));
    }

    [HttpGet("submissions/{submissionId}/download")]
    public async Task<IActionResult> DownloadSubmission(
        int submissionId,
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken)
            || _currentUser.UserId is not int)
        {
            return Unauthorized();
        }

        var submission = await _dbContext.Submissions
            .AsNoTracking()
            .Include(s => s.Student)
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Allocation)
                    .ThenInclude(a => a.Staff)
                        .ThenInclude(st => st.Department)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId, cancellationToken);
        if (submission is null) return NotFound();

        var role = RoleNames.ToPolicyRole(_currentUser.Role);
        var allowed = role.Equals(RoleNames.RegistrarAdmin, StringComparison.OrdinalIgnoreCase);

        if (role.Equals(RoleNames.Student, StringComparison.OrdinalIgnoreCase))
        {
            allowed = _currentUser.StudentId is int studentId
                && submission.StudentId == studentId;
        }
        else if (role.Equals(RoleNames.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            allowed = _currentUser.StaffId is int staffId
                && submission.Assignment.Allocation.StaffId == staffId;
        }
        else if (role.Equals(RoleNames.HoD, StringComparison.OrdinalIgnoreCase))
        {
            if (_currentUser.StaffId is int staffId)
            {
                var departmentId = await _dbContext.Staff
                    .AsNoTracking()
                    .Where(st => st.StaffId == staffId)
                    .Select(st => (int?)st.DepartmentId)
                    .FirstOrDefaultAsync(cancellationToken);

                allowed = departmentId is int currentDepartmentId
                    && submission.Assignment.Allocation.Staff.DepartmentId == currentDepartmentId;
            }
        }

        if (!allowed) return Forbid();

        Stream content;
        try
        {
            content = await _fileStorage.OpenReadAsync(
                submission.FileUrl,
                cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound();
        }

        return File(
            content,
            string.IsNullOrWhiteSpace(submission.ContentType)
                ? "application/octet-stream"
                : submission.ContentType,
            string.IsNullOrWhiteSpace(submission.OriginalFileName)
                ? Path.GetFileName(submission.FileUrl)
                : submission.OriginalFileName,
            enableRangeProcessing: true);
    }

    [Authorize(Policy = "LecturerOnly")]
    [HttpPut("submissions/{id}/grade")]
    public async Task<ActionResult<SubmissionRecord>> Grade(int id, [FromBody] GradeRequest request)
    {
        var submission = await SubmissionGraph()
            .FirstOrDefaultAsync(s => s.SubmissionId == id);
        if (submission is null) return NotFound();

        var assignment = await _dbContext.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssignmentId == submission.AssignmentId);
        if (assignment is null) return NotFound();
        if (request.MarksAwarded < 0 || request.MarksAwarded > assignment.MaxMarks)
        {
            return BadRequest($"Marks must be between 0 and {assignment.MaxMarks}.");
        }

        submission.MarksAwarded = request.MarksAwarded;
        submission.Feedback = request.Feedback?.Trim() ?? "";

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(ToSubmissionRecord(submission));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> GetSummary([FromQuery] string? studentId)
    {
        if (studentId is not null)
        {
            var student = await FindStudentByNumberAsync(studentId);
            if (student is null)
            {
                return Ok(new { assignmentCount = 0, submittedCount = 0, pendingCount = 0 });
            }

            var allowed = await AllocationIdsForStudent(student.StudentNumber);
            var mine = await _dbContext.Assignments
                .AsNoTracking()
                .Where(a => allowed.Contains(a.AllocationId))
                .Select(a => a.AssignmentId)
                .ToListAsync();
            var submittedIds = await _dbContext.Submissions
                .AsNoTracking()
                .Where(s => s.StudentId == student.StudentId && mine.Contains(s.AssignmentId))
                .Select(s => s.AssignmentId)
                .ToListAsync();
            var submitted = submittedIds.Count;
            var pending = mine.Count(assignmentId => !submittedIds.Contains(assignmentId));
            return Ok(new { assignmentCount = mine.Count, submittedCount = submitted, pendingCount = pending });
        }

        var ungraded = await _dbContext.Submissions.CountAsync(s => s.MarksAwarded == null);
        var assignmentCount = await _dbContext.Assignments.CountAsync();
        var submissionCount = await _dbContext.Submissions.CountAsync();
        return Ok(new
        {
            assignmentCount,
            submissionCount,
            pendingGradingCount = ungraded,
        });
    }

    private async Task<SavedFile> SaveFileAsync(
        IFormFile? file,
        string subfolder,
        CancellationToken cancellationToken)
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

        await using var input = file.OpenReadStream();
        var stored = await _fileStorage.SaveAsync(
            input,
            subfolder == "materials" ? "learning-materials" : "learning-submissions",
            ext,
            file.FileName,
            file.ContentType,
            cancellationToken);

        return new SavedFile
        {
            Path = stored.StorageKey,
            OriginalName = stored.OriginalFileName,
            ContentType = stored.ContentType,
            Length = stored.Length,
        };
    }

    private async Task<CourseAllocation?> LoadAllocation(int allocationId)
    {
        return await _dbContext.CourseAllocations
            .AsNoTracking()
            .Include(a => a.Course)
            .FirstOrDefaultAsync(a => a.AllocationId == allocationId);
    }

    private IQueryable<Assignment> AssignmentGraph()
    {
        return _dbContext.Assignments
            .Include(a => a.Allocation)
                .ThenInclude(al => al.Course);
    }

    private IQueryable<Submission> SubmissionGraph()
    {
        return _dbContext.Submissions
            .Include(s => s.Student)
                .ThenInclude(st => st.Admission);
    }

    private async Task<Student?> FindStudentByNumberAsync(string studentNumber)
    {
        return await _dbContext.Students
            .AsNoTracking()
            .Include(s => s.Admission)
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);
    }

    private SubmissionRecord ToSubmissionRecord(Submission submission)
    {
        var number = submission.Student.StudentNumber;
        return new SubmissionRecord
        {
            Id = submission.SubmissionId,
            AssignmentId = submission.AssignmentId,
            StudentId = number,
            StudentName = submission.Student.Admission?.ApplicantName ?? number,
            FileUrl = submission.FileUrl,
            FileName = string.IsNullOrWhiteSpace(submission.OriginalFileName)
                ? Path.GetFileName(submission.FileUrl)
                : submission.OriginalFileName,
            SubmittedAt = submission.SubmittedAt,
            IsLate = submission.IsLate,
            MarksAwarded = submission.MarksAwarded,
            Feedback = submission.Feedback,
        };
    }

    private AssignmentRecord ToAssignmentRecord(Assignment assignment)
    {
        var course = assignment.Allocation.Course;
        return new AssignmentRecord
        {
            Id = assignment.AssignmentId,
            AllocationId = assignment.AllocationId,
            CourseId = course.CourseId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            Title = assignment.Title,
            Instructions = assignment.Instructions ?? "",
            DueDate = FormatDueDate(assignment.DueDate),
            MaxMarks = assignment.MaxMarks,
            AllowLateSubmissions = assignment.AllowLateSubmissions,
        };
    }

    private static bool TryParseDueDate(string? value, out DateTime dueDate)
    {
        dueDate = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dueDate))
        {
            return false;
        }

        if (dueDate.Kind == DateTimeKind.Unspecified)
        {
            dueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc);
        }
        else if (dueDate.Kind == DateTimeKind.Local)
        {
            dueDate = dueDate.ToUniversalTime();
        }

        return true;
    }

    private static string FormatDueDate(DateTime dueDate)
    {
        var utc = dueDate.Kind == DateTimeKind.Utc
            ? dueDate
            : DateTime.SpecifyKind(dueDate, DateTimeKind.Utc);
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the learning record.";

        if (ex.InnerException is not SqlException sql)
        {
            return false;
        }

        if (sql.Number is 2601 or 2627)
        {
            status = StatusCodes.Status409Conflict;
            var detail = sql.Message;
            if (detail.Contains("UQ_submissions", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("submissions", StringComparison.OrdinalIgnoreCase))
            {
                message = "A submission already exists for this student and assignment.";
            }
            else
            {
                message = "That record could not be saved because it conflicts with an existing row.";
            }
            return true;
        }

        if (sql.Number == 547)
        {
            message = "That record could not be saved because a related row is missing or still in use.";
            return true;
        }

        return false;
    }

    private async Task<HashSet<int>> AllocationIdsForStudent(string studentId)
    {
        var ids = await _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.Status == "Approved" && r.Student.StudentNumber == studentId)
            .Select(r => r.AllocationId)
            .ToListAsync();

        return ids.ToHashSet();
    }

    private async Task<List<AttendanceRosterStudent>> RosterFor(int allocationId)
    {
        var roster = await _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.AllocationId == allocationId && r.Status == "Approved")
            .Select(r => new AttendanceRosterStudent
            {
                StudentId = r.Student.StudentNumber,
                StudentName = r.Student.Admission != null
                    ? r.Student.Admission.ApplicantName
                    : r.Student.StudentNumber,
            })
            .ToListAsync();

        return roster
            .DistinctBy(s => s.StudentId)
            .ToList();
    }

    private static LearningMaterialRecord ToMaterialRecord(LearningMaterial material)
    {
        var course = material.Allocation.Course;
        return new LearningMaterialRecord
        {
            Id = material.LearningMaterialId,
            AllocationId = material.AllocationId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            Title = material.Title,
            Path = material.StorageKey,
            FileName = material.OriginalFileName,
            ContentType = material.ContentType,
            UploadedAt = material.UploadedAt,
        };
    }

    private class SavedFile
    {
        public string? Path { get; set; }
        public string? OriginalName { get; set; }
        public string? ContentType { get; set; }
        public long Length { get; set; }
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
    public string? ContentType { get; set; }
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

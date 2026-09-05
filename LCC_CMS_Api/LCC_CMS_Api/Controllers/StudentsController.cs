using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M2 — Student Profile Management (Module Specification).
///
/// Reads and corrects profiles from <c>students</c>, <c>users</c>, and
/// <c>programmes</c>. Display name, phone, date of birth, and gender are
/// taken from the linked <c>admissions</c> row created on approval.
/// Creation still happens in M1 — this controller does not insert students.
///
/// Public identifier is <c>StudentNumber</c> (e.g. LCC-24001), not the
/// integer <c>StudentId</c> shared with <c>users.user_id</c>.
///
/// [Authorize(Policy = "StudentOnly")] and [Authorize(Policy =
/// "RegistrarAdminOnly")] go back on the relevant endpoints once
/// AuthEnabled=true. /me uses ICurrentUser (lab: X-User-Id).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    internal const string ProfilePhotoType = "Profile Photo";

    private static readonly string[] AllowedPhotoExtensions = { ".jpg", ".jpeg", ".png" };
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _fileStorage;

    public StudentsController(
        LccCmsDbContext dbContext,
        ICurrentUser currentUser,
        IFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    // --- Self-service (Student role) ---

    // [Authorize(Policy = "StudentOnly")] — re-enable once AuthEnabled=true
    [HttpGet("me")]
    public async Task<ActionResult<StudentProfile>> GetMyProfile(CancellationToken cancellationToken)
    {
        var loaded = await LoadCurrentStudentAsync(cancellationToken);
        if (loaded.Error is not null) return loaded.Error;
        return Ok(ToProfile(loaded.Student!));
    }

    // [Authorize(Policy = "StudentOnly")]
    [HttpPut("me")]
    public async Task<ActionResult<StudentProfile>> UpdateMyProfile(
        [FromBody] StudentProfileEditRequest request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadCurrentStudentAsync(cancellationToken);
        if (loaded.Error is not null) return loaded.Error;

        ApplyEdits(loaded.Student!, request);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToProfile(loaded.Student!));
    }

    // [Authorize(Policy = "StudentOnly")]
    [HttpPost("me/photo")]
    [RequestSizeLimit(MaxPhotoSizeBytes + 1024)]
    public async Task<ActionResult<StudentProfile>> UploadMyPhoto(
        IFormFile? photo,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadCurrentStudentAsync(cancellationToken);
        if (loaded.Error is not null) return loaded.Error;

        var saved = await SavePhotoAsync(photo, cancellationToken);
        if (saved.Error is not null) return BadRequest(saved.Error);

        UpsertProfilePhoto(loaded.Student!, saved.Path!, photo?.ContentType);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToProfile(loaded.Student!));
    }

    [HttpGet("me/photo")]
    public async Task<IActionResult> DownloadMyPhoto(CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken)
            || _currentUser.UserId is not int)
        {
            return Unauthorized();
        }

        if (_currentUser.StudentId is not int studentId)
        {
            return NotFound();
        }

        return await DownloadPhotoForStudentAsync(studentId, cancellationToken);
    }

    [HttpGet("{studentNumber}/photo")]
    public async Task<IActionResult> DownloadPhoto(
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
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber, cancellationToken);
        if (student is null) return NotFound();

        var role = RoleNames.ToPolicyRole(_currentUser.Role);
        var allowed = role.Equals(RoleNames.RegistrarAdmin, StringComparison.OrdinalIgnoreCase)
            || (role.Equals(RoleNames.Student, StringComparison.OrdinalIgnoreCase)
                && _currentUser.StudentId == student.StudentId);
        if (!allowed) return Forbid();

        return await DownloadPhotoForStudentAsync(student.StudentId, cancellationToken);
    }

    // --- Registrar/Admin oversight ---

    // [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StudentProfile>>> GetAll()
    {
        var students = await StudentGraph()
            .AsNoTracking()
            .OrderBy(s => s.StudentNumber)
            .ToListAsync();

        return Ok(students.Select(ToProfile));
    }

    // [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPut("{id}")]
    public async Task<ActionResult<StudentProfile>> CorrectProfile(string id, [FromBody] StudentProfileEditRequest request)
    {
        var student = await StudentGraph()
            .FirstOrDefaultAsync(s => s.StudentNumber == id);
        if (student is null) return NotFound();

        ApplyEdits(student, request);
        await _dbContext.SaveChangesAsync();
        return Ok(ToProfile(student));
    }

    private IQueryable<Student> StudentGraph()
    {
        return _dbContext.Students
            .Include(s => s.StudentNavigation)
            .Include(s => s.Programme)
            .Include(s => s.Admission)
            .Include(s => s.Documents);
    }

    private async Task<(Student? Student, ActionResult? Error)> LoadCurrentStudentAsync(
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken))
        {
            return (null, Unauthorized());
        }

        if (_currentUser.StudentId is not int studentId)
        {
            return (null, NotFound());
        }

        var query = StudentGraph().Where(s => s.StudentId == studentId);
        if (!string.IsNullOrEmpty(_currentUser.StudentNumber))
        {
            query = query.Where(s => s.StudentNumber == _currentUser.StudentNumber);
        }

        var student = await query.FirstOrDefaultAsync(cancellationToken);
        if (student is null)
        {
            return (null, NotFound());
        }

        return (student, null);
    }

    private static void ApplyEdits(Student student, StudentProfileEditRequest request)
    {
        var name = request.EmergencyContactName?.Trim() ?? "";
        var phone = request.EmergencyContactPhone?.Trim() ?? "";
        var combined = string.IsNullOrEmpty(name) && string.IsNullOrEmpty(phone)
            ? null
            : $"{name}|{phone}";
        if (combined is not null && combined.Length > 255)
        {
            combined = combined[..255];
        }
        student.EmergencyContact = combined;

        if (student.Admission is not null)
        {
            var applicantPhone = request.Phone?.Trim() ?? "";
            student.Admission.ApplicantPhone = applicantPhone.Length == 0
                ? null
                : applicantPhone.Length > 30 ? applicantPhone[..30] : applicantPhone;
        }
    }

    private async Task<SavedPhoto> SavePhotoAsync(
        IFormFile? photo,
        CancellationToken cancellationToken)
    {
        if (photo is null)
        {
            return new SavedPhoto { Error = "No photo file received." };
        }

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!AllowedPhotoExtensions.Contains(ext))
        {
            return new SavedPhoto { Error = $"File type '{ext}' not allowed. Use JPG or PNG." };
        }
        if (photo.Length > MaxPhotoSizeBytes)
        {
            return new SavedPhoto { Error = "Photo is too large. Maximum size is 5 MB." };
        }

        await using var input = photo.OpenReadStream();
        var stored = await _fileStorage.SaveAsync(
            input,
            "students",
            ext,
            photo.FileName,
            photo.ContentType,
            cancellationToken);

        return new SavedPhoto { Path = stored.StorageKey };
    }

    private static void UpsertProfilePhoto(
        Student student,
        string fileUrl,
        string? contentType)
    {
        var existing = student.Documents.FirstOrDefault(d => d.DocumentType == ProfilePhotoType);
        if (existing is null)
        {
            student.Documents.Add(new Document
            {
                StudentId = student.StudentId,
                DocumentType = ProfilePhotoType,
                FileUrl = fileUrl,
                ContentType = contentType,
                UploadedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.FileUrl = fileUrl;
            existing.ContentType = contentType;
            existing.UploadedAt = DateTime.UtcNow;
        }
    }

    private async Task<IActionResult> DownloadPhotoForStudentAsync(
        int studentId,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.StudentId == studentId && d.DocumentType == ProfilePhotoType)
            .OrderByDescending(d => d.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null) return NotFound();

        Stream content;
        try
        {
            content = await _fileStorage.OpenReadAsync(document.FileUrl, cancellationToken);
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
            string.IsNullOrWhiteSpace(document.ContentType)
                ? "application/octet-stream"
                : document.ContentType,
            Path.GetFileName(document.FileUrl),
            enableRangeProcessing: true);
    }

    private static StudentProfile ToProfile(Student student)
    {
        var emergency = student.EmergencyContact ?? "";
        var pipe = emergency.IndexOf('|');
        var emergencyName = pipe < 0 ? emergency : emergency[..pipe];
        var emergencyPhone = pipe < 0 ? "" : emergency[(pipe + 1)..];

        var photo = student.Documents
            .Where(d => d.DocumentType == ProfilePhotoType)
            .OrderByDescending(d => d.UploadedAt)
            .FirstOrDefault();

        return new StudentProfile
        {
            Id = student.StudentNumber,
            FullName = student.Admission?.ApplicantName ?? "",
            Email = student.StudentNavigation?.Email ?? "",
            Phone = student.Admission?.ApplicantPhone ?? "",
            Programme = student.Programme?.ProgrammeName ?? "",
            DateOfBirth = student.Admission?.DateOfBirth?.ToString("yyyy-MM-dd") ?? "",
            Gender = student.Admission?.Gender ?? "",
            MaritalStatus = "",
            Province = "",
            District = "",
            Village = "",
            PostalAddress = "",
            EmergencyContactName = emergencyName,
            EmergencyContactPhone = emergencyPhone,
            PhotoPath = photo?.FileUrl,
            PhotoFileName = photo is null ? null : Path.GetFileName(photo.FileUrl),
        };
    }

    private class SavedPhoto
    {
        public string? Path { get; set; }
        public string? Error { get; set; }
    }
}

/// <summary>
/// Name lookup for Attendance/Learning, keyed by public StudentNumber.
/// </summary>
internal static class StudentDirectory
{
    public static async Task<string> DisplayNameAsync(LccCmsDbContext dbContext, string studentNumber)
    {
        var name = await dbContext.Students
            .AsNoTracking()
            .Where(s => s.StudentNumber == studentNumber)
            .Select(s => s.Admission != null ? s.Admission.ApplicantName : s.StudentNumber)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(name) ? studentNumber : name;
    }
}

public class StudentProfile
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Programme { get; set; } = "";
    public string DateOfBirth { get; set; } = "";
    public string Gender { get; set; } = "";
    public string MaritalStatus { get; set; } = "";
    public string Province { get; set; } = "";
    public string District { get; set; } = "";
    public string Village { get; set; } = "";
    public string PostalAddress { get; set; } = "";
    public string EmergencyContactName { get; set; } = "";
    public string EmergencyContactPhone { get; set; } = "";
    public string? PhotoPath { get; set; }
    public string? PhotoFileName { get; set; }
}

public class StudentProfileEditRequest
{
    public string Phone { get; set; } = "";
    public string EmergencyContactName { get; set; } = "";
    public string EmergencyContactPhone { get; set; } = "";
    public string PostalAddress { get; set; } = "";
}

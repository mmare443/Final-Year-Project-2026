using LCC_CMS_Api.Models;
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
/// AuthEnabled=true. Until then, /me is the first student by student number.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    internal const string ProfilePhotoType = "Profile Photo";

    private static readonly string[] AllowedPhotoExtensions = { ".jpg", ".jpeg", ".png" };
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IWebHostEnvironment _env;
    private readonly LccCmsDbContext _dbContext;

    public StudentsController(IWebHostEnvironment env, LccCmsDbContext dbContext)
    {
        _env = env;
        _dbContext = dbContext;
    }

    // --- Self-service (Student role) ---

    // [Authorize(Policy = "StudentOnly")] — re-enable once AuthEnabled=true
    [HttpGet("me")]
    public async Task<ActionResult<StudentProfile>> GetMyProfile()
    {
        var student = await LoadCurrentStudentAsync();
        if (student is null) return NotFound();
        return Ok(ToProfile(student));
    }

    // [Authorize(Policy = "StudentOnly")]
    [HttpPut("me")]
    public async Task<ActionResult<StudentProfile>> UpdateMyProfile([FromBody] StudentProfileEditRequest request)
    {
        var student = await LoadCurrentStudentAsync();
        if (student is null) return NotFound();

        ApplyEdits(student, request);
        await _dbContext.SaveChangesAsync();
        return Ok(ToProfile(student));
    }

    // [Authorize(Policy = "StudentOnly")]
    [HttpPost("me/photo")]
    [RequestSizeLimit(MaxPhotoSizeBytes + 1024)]
    public async Task<ActionResult<StudentProfile>> UploadMyPhoto(IFormFile? photo)
    {
        var student = await LoadCurrentStudentAsync();
        if (student is null) return NotFound();

        var saved = SavePhoto(photo);
        if (saved.Error is not null) return BadRequest(saved.Error);

        UpsertProfilePhoto(student, saved.Path!);
        await _dbContext.SaveChangesAsync();
        return Ok(ToProfile(student));
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

    private async Task<Student?> LoadCurrentStudentAsync()
    {
        // AuthEnabled=false: no Entra identity yet — first student by number.
        return await StudentGraph()
            .OrderBy(s => s.StudentNumber)
            .FirstOrDefaultAsync();
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

    private SavedPhoto SavePhoto(IFormFile? photo)
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

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "students");
        Directory.CreateDirectory(uploadsFolder);

        var safeFileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadsFolder, safeFileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            photo.CopyTo(stream);
        }

        return new SavedPhoto { Path = $"/uploads/students/{safeFileName}" };
    }

    private static void UpsertProfilePhoto(Student student, string fileUrl)
    {
        var existing = student.Documents.FirstOrDefault(d => d.DocumentType == ProfilePhotoType);
        if (existing is null)
        {
            student.Documents.Add(new Document
            {
                StudentId = student.StudentId,
                DocumentType = ProfilePhotoType,
                FileUrl = fileUrl,
                UploadedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.FileUrl = fileUrl;
            existing.UploadedAt = DateTime.UtcNow;
        }
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

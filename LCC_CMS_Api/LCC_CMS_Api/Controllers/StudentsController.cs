using Microsoft.AspNetCore.Mvc;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M2 — Student Profile Management (Module Specification).
///
/// SKELETON: in-memory placeholder data, same pattern as AdmissionsController.
/// One demo student profile is seeded on startup so the self-service flow
/// (GET/PUT /me) has something real to view and edit immediately.
///
/// RBAC NOTE: per the Module Spec's business rule ("a student may view and
/// maintain their own profile only"), the /me endpoints should resolve
/// "me" from the signed-in user's real identity once Entra ID auth is
/// live — right now, with AuthEnabled=false, /me always resolves to the
/// single seeded demo profile since there's no real per-user session yet.
/// [Authorize(Policy = "StudentOnly")] and [Authorize(Policy =
/// "RegistrarAdminOnly")] go back on the relevant endpoints below once
/// AuthEnabled=true.
///
/// TODO once EF Core / LccCmsDbContext exists: replace the in-memory
/// List<> with real queries against `students` and `users` (Read/Update
/// per the spec — profile records are corrected, not created, here;
/// creation happens in M1 on admission approval).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    // Internal so M5 (Attendance) can resolve names for the class roster
    // without a real FK — same in-memory-stage pattern as M3/M4.
    internal static readonly List<StudentProfile> _students = new()
    {
        new StudentProfile
        {
            Id = "LCC-24001",
            FullName = "Mond Mare",
            Email = "test.student@lccb.ac.pg",
            Phone = "73873263",
            Programme = "Diploma in Business Administration and Management",
            DateOfBirth = "2002-03-14",
            Gender = "Male",
            MaritalStatus = "Single",
            Province = "Jiwaka",
            District = "North Waghi",
            Village = "Banz",
            PostalAddress = "P.O. Box 72, Mt. Hagen, Western Highlands Province",
            EmergencyContactName = "",
            EmergencyContactPhone = "",
            PhotoPath = null,
            PhotoFileName = null,
        },
        new StudentProfile
        {
            Id = "LCC-24002",
            FullName = "Sarah Kuman",
            Email = "sarah.kuman@lccb.ac.pg",
            Phone = "72001002",
            Programme = "Diploma in Business Administration and Management",
            DateOfBirth = "2003-07-22",
            Gender = "Female",
            MaritalStatus = "Single",
            Province = "Jiwaka",
            District = "Anglimp South Waghi",
            Village = "Banz",
            PostalAddress = "P.O. Box 72, Mt. Hagen, Western Highlands Province",
        },
        new StudentProfile
        {
            Id = "LCC-24003",
            FullName = "Peter Namba",
            Email = "peter.namba@lccb.ac.pg",
            Phone = "72001003",
            Programme = "Diploma in Business Administration and Management",
            DateOfBirth = "2001-11-08",
            Gender = "Male",
            MaritalStatus = "Single",
            Province = "Western Highlands",
            District = "Hagen Central",
            Village = "Kagamuga",
            PostalAddress = "P.O. Box 72, Mt. Hagen, Western Highlands Province",
        },
        new StudentProfile
        {
            Id = "LCC-24004",
            FullName = "Agnes Wemin",
            Email = "agnes.wemin@lccb.ac.pg",
            Phone = "72001004",
            Programme = "Diploma in Business Administration and Management",
            DateOfBirth = "2004-01-30",
            Gender = "Female",
            MaritalStatus = "Single",
            Province = "Jiwaka",
            District = "North Waghi",
            Village = "Minj",
            PostalAddress = "P.O. Box 72, Mt. Hagen, Western Highlands Province",
        },
    };

    private static readonly string[] AllowedPhotoExtensions = { ".jpg", ".jpeg", ".png" };
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IWebHostEnvironment _env;

    public StudentsController(IWebHostEnvironment env)
    {
        _env = env;
    }

    // --- Self-service (Student role) ---

    // [Authorize(Policy = "StudentOnly")] — re-enable once AuthEnabled=true
    [HttpGet("me")]
    public ActionResult<StudentProfile> GetMyProfile()
    {
        // See the RBAC NOTE above — "me" is the single seeded profile for now.
        return Ok(_students[0]);
    }

    // [Authorize(Policy = "StudentOnly")]
    [HttpPut("me")]
    public ActionResult<StudentProfile> UpdateMyProfile([FromBody] StudentProfileEditRequest request)
    {
        var student = _students[0];
        student.Phone = request.Phone;
        student.EmergencyContactName = request.EmergencyContactName;
        student.EmergencyContactPhone = request.EmergencyContactPhone;
        student.PostalAddress = request.PostalAddress;
        return Ok(student);
    }

    // [Authorize(Policy = "StudentOnly")]
    [HttpPost("me/photo")]
    [RequestSizeLimit(MaxPhotoSizeBytes + 1024)]
    public ActionResult<StudentProfile> UploadMyPhoto(IFormFile? photo)
    {
        if (photo is null)
        {
            return BadRequest("No photo file received.");
        }

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!AllowedPhotoExtensions.Contains(ext))
        {
            return BadRequest($"File type '{ext}' not allowed. Use JPG or PNG.");
        }
        if (photo.Length > MaxPhotoSizeBytes)
        {
            return BadRequest("Photo is too large. Maximum size is 5 MB.");
        }

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "students");
        Directory.CreateDirectory(uploadsFolder);

        var safeFileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadsFolder, safeFileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            photo.CopyTo(stream);
        }

        var student = _students[0];
        student.PhotoPath = $"/uploads/students/{safeFileName}";
        student.PhotoFileName = photo.FileName;

        return Ok(student);
    }

    // --- Registrar/Admin oversight ---

    // [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpGet]
    public ActionResult<IEnumerable<StudentProfile>> GetAll()
    {
        return Ok(_students);
    }

    // [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPut("{id}")]
    public ActionResult<StudentProfile> CorrectProfile(string id, [FromBody] StudentProfileEditRequest request)
    {
        var student = _students.FirstOrDefault(s => s.Id == id);
        if (student is null) return NotFound();

        student.Phone = request.Phone;
        student.EmergencyContactName = request.EmergencyContactName;
        student.EmergencyContactPhone = request.EmergencyContactPhone;
        student.PostalAddress = request.PostalAddress;
        return Ok(student);
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

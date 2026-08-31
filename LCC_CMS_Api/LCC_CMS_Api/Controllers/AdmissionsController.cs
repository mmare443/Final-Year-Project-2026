using Microsoft.AspNetCore.Mvc;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M1 — Admissions, Enrolment & Readmission (Module Specification).
///
/// SKELETON ONLY: returns in-memory placeholder data matching the shape
/// the React SPA's MockDataContext already uses, so swapping the frontend
/// from mock data to real fetch() calls is a data-source change, not a
/// shape change.
///
/// TODO once EF Core / LccCmsDbContext exists (Backend Scaffold Guide v2,
/// Step 2): replace the static list below with real queries against the
/// `admissions`, `users`, `students`, and `programmes` tables per the
/// Module Specification's Associated Database Entities for M1. Documents
/// should become their own table (one application has many documents),
/// not columns on the admissions row itself.
///
/// [Authorize(Policy = "RegistrarAdminOnly")] goes back on the decision
/// endpoint once AuthEnabled=true in appsettings.Development.json.
///
/// FILE UPLOAD NOTE: uploaded documents are saved to wwwroot/uploads/admissions
/// on local disk — fine for development, NOT how this should work once
/// deployed. TODO once cloud access exists: swap the local File.WriteAllBytes
/// call below for an upload to Azure Blob Storage or GCS, and store the
/// resulting blob URL in each document's Path instead of a local relative
/// path. The rest of the code (validation, the document list itself)
/// doesn't need to change.
///
/// DOCUMENT SET: matches Section 8 (the submission checklist) of LCCB's
/// actual paper Application Form for 2027 Enrolment — 7 required documents
/// plus 1 conditional one (Work Reference, only relevant if the applicant
/// has listed work experience in Section 5 of the paper form).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AdmissionsController : ControllerBase
{
    // In-memory placeholder store — resets whenever the API restarts.
    private static readonly List<AdmissionApplication> _applications = new();
    private static int _nextId = 1;
    private static int _nextStudentId = 24001;

    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB per file

    private readonly IWebHostEnvironment _env;

    public AdmissionsController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet]
    public ActionResult<IEnumerable<AdmissionApplication>> GetAll()
    {
        return Ok(_applications);
    }

    // Changed from [FromBody] JSON to [FromForm] multipart — required
    // whenever a file is part of the request. The React side sends this
    // as FormData, not JSON.stringify, to match.
    [HttpPost]
    [RequestSizeLimit(8 * MaxFileSizeBytes)] // headroom for up to 8 files
    public ActionResult<AdmissionApplication> Submit([FromForm] AdmissionApplicationRequest request)
    {
        var documents = new List<AdmissionDocument>();

        // (property, document type label, required per Section 8 checklist)
        var fileFields = new (IFormFile? File, string Type, bool Required)[]
        {
            (request.LetterOfInterest, "Letter of Interest", true),
            (request.PassportPhoto, "Passport Photo", true),
            (request.FeeDepositSlip, "Fee Deposit Slip (K30)", true),
            (request.Grade10Certificate, "Grade 10 Certificate", true),
            (request.Grade12Certificate, "Grade 12 Certificate", true),
            (request.ReferenceLetter1, "Reference Letter — Church Pastor", true),
            (request.ReferenceLetter2, "Reference Letter — Community Leader", true),
            (request.WorkReference, "Work Reference", false),
        };

        var missingRequired = fileFields.Where(f => f.Required && f.File is null).Select(f => f.Type).ToList();
        if (missingRequired.Count > 0)
        {
            return BadRequest($"Missing required documents: {string.Join(", ", missingRequired)}.");
        }

        foreach (var (file, type, _) in fileFields)
        {
            if (file is null) continue; // only WorkReference can legitimately be null here

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                return BadRequest($"{type}: file type '{ext}' not allowed. Use PDF, JPG, or PNG.");
            }
            if (file.Length > MaxFileSizeBytes)
            {
                return BadRequest($"{type}: file is too large. Maximum size is 5 MB.");
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "admissions");
            Directory.CreateDirectory(uploadsFolder);

            // Guid-prefixed filename — never trust the original filename
            // for storage (path traversal, collisions, unsafe characters).
            var safeFileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadsFolder, safeFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            documents.Add(new AdmissionDocument
            {
                Type = type,
                Path = $"/uploads/admissions/{safeFileName}",
                FileName = file.FileName, // original name, for display only
            });
        }

        var application = new AdmissionApplication
        {
            Id = _nextId++,
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            Programme = request.Programme,
            Status = "Applied",
            SubmittedAt = DateTime.UtcNow,
            Documents = documents,
        };

        _applications.Add(application);
        return CreatedAtAction(nameof(GetAll), new { id = application.Id }, application);
    }

    // [Authorize(Policy = "RegistrarAdminOnly")] — re-enable once AuthEnabled=true
    [HttpPatch("{id}/decision")]
    public ActionResult<AdmissionApplication> Decide(int id, [FromBody] AdmissionDecisionRequest request)
    {
        var application = _applications.FirstOrDefault(a => a.Id == id);
        if (application is null) return NotFound();

        if (request.Decision == "approve")
        {
            application.Status = "Approved";
            application.StudentId = $"LCC-{_nextStudentId++}";
            // TODO: real Entra ID account provisioning + welcome email
            // (Module Spec M1, process steps 4-5) happens here once
            // Entra ID app registrations exist.
        }
        else if (request.Decision == "reject")
        {
            application.Status = "Rejected";
        }
        else
        {
            return BadRequest("Decision must be 'approve' or 'reject'.");
        }

        return Ok(application);
    }
}

public class AdmissionApplication
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Programme { get; set; } = "";
    public string Status { get; set; } = "";
    public string? StudentId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public List<AdmissionDocument> Documents { get; set; } = new();
}

public class AdmissionDocument
{
    public string Type { get; set; } = "";     // e.g. "Grade 12 Certificate"
    public string Path { get; set; } = "";     // e.g. /uploads/admissions/<guid>.pdf
    public string FileName { get; set; } = ""; // original filename, display only
}

public class AdmissionApplicationRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Programme { get; set; } = "";

    // Matches Section 8 of LCCB's paper Application Form checklist.
    public IFormFile? LetterOfInterest { get; set; }
    public IFormFile? PassportPhoto { get; set; }
    public IFormFile? FeeDepositSlip { get; set; }
    public IFormFile? Grade10Certificate { get; set; }
    public IFormFile? Grade12Certificate { get; set; }
    public IFormFile? ReferenceLetter1 { get; set; }
    public IFormFile? ReferenceLetter2 { get; set; }
    public IFormFile? WorkReference { get; set; } // optional — only if applicable
}

public class AdmissionDecisionRequest
{
    public string Decision { get; set; } = ""; // "approve" | "reject"
}

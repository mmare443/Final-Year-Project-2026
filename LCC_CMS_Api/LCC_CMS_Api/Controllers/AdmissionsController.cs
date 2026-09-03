using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M1 — Admissions, Enrolment &amp; Readmission (Module Specification).
///
/// Phase 1: list and submit persist through EF Core (<c>admissions</c> +
/// <c>programmes</c>). Uploaded files still land on
/// wwwroot/uploads/admissions; document metadata is held in a process-local
/// sidecar until approval (Phase 3) creates a <c>students</c> row that
/// <c>documents.student_id</c> can reference.
///
/// PATCH /{id}/decision is unchanged in route only — it does not create
/// users/students or persist a decision yet.
///
/// [Authorize(Policy = "RegistrarAdminOnly")] goes back on the decision
/// endpoint once AuthEnabled=true in appsettings.Development.json.
///
/// DOCUMENT SET: matches Section 8 (the submission checklist) of LCCB's
/// actual paper Application Form for 2027 Enrolment — 7 required documents
/// plus 1 conditional one (Work Reference).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AdmissionsController : ControllerBase
{
    // Document metadata cannot go on admissions and cannot go in documents
    // until a student exists. Sidecar is keyed by AdmissionId.
    private static readonly Dictionary<int, List<AdmissionDocument>> _documentsByAdmissionId = new();

    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB per file

    private readonly IWebHostEnvironment _env;
    private readonly LccCmsDbContext _dbContext;

    public AdmissionsController(IWebHostEnvironment env, LccCmsDbContext dbContext)
    {
        _env = env;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdmissionApplication>>> GetAll()
    {
        var admissions = await _dbContext.Admissions
            .AsNoTracking()
            .Include(a => a.Programme)
            .Include(a => a.Student)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(admissions.Select(ToApplication));
    }

    // Changed from [FromBody] JSON to [FromForm] multipart — required
    // whenever a file is part of the request. The React side sends this
    // as FormData, not JSON.stringify, to match.
    [HttpPost]
    [RequestSizeLimit(8 * MaxFileSizeBytes)] // headroom for up to 8 files
    public async Task<ActionResult<AdmissionApplication>> Submit([FromForm] AdmissionApplicationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Email is required.");
        }

        if (request.FullName.Trim().Length > 150)
        {
            return BadRequest("Full name must be 150 characters or fewer.");
        }

        if (request.Email.Trim().Length > 255)
        {
            return BadRequest("Email must be 255 characters or fewer.");
        }

        if (!string.IsNullOrWhiteSpace(request.Phone) && request.Phone.Trim().Length > 30)
        {
            return BadRequest("Phone must be 30 characters or fewer.");
        }

        var programme = await ResolveProgramme(request);
        if (programme is null)
        {
            return BadRequest("Programme not found.");
        }

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

        var admission = new Admission
        {
            ApplicantName = request.FullName.Trim(),
            ApplicantEmail = request.Email.Trim(),
            ApplicantPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            ProgrammeId = programme.ProgrammeId,
            Status = "Applied",
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Admissions.Add(admission);
        await _dbContext.SaveChangesAsync();

        _documentsByAdmissionId[admission.AdmissionId] = documents;

        var created = ToApplication(admission);
        created.Programme = programme.ProgrammeName;
        return CreatedAtAction(nameof(GetAll), new { id = admission.AdmissionId }, created);
    }

    // [Authorize(Policy = "RegistrarAdminOnly")] — re-enable once AuthEnabled=true
    // Approval workflow (user + student + document rows) is not implemented yet.
    [HttpPatch("{id}/decision")]
    public async Task<ActionResult<AdmissionApplication>> Decide(int id, [FromBody] AdmissionDecisionRequest request)
    {
        var admission = await _dbContext.Admissions
            .AsNoTracking()
            .Include(a => a.Programme)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.AdmissionId == id);
        if (admission is null) return NotFound();

        if (request.Decision != "approve" && request.Decision != "reject")
        {
            return BadRequest("Decision must be 'approve' or 'reject'.");
        }

        return Ok(ToApplication(admission));
    }

    private async Task<Programme?> ResolveProgramme(AdmissionApplicationRequest request)
    {
        if (request.ProgrammeId > 0)
        {
            return await _dbContext.Programmes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProgrammeId == request.ProgrammeId);
        }

        if (string.IsNullOrWhiteSpace(request.Programme))
        {
            return null;
        }

        return await _dbContext.Programmes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProgrammeName == request.Programme);
    }

    private AdmissionApplication ToApplication(Admission admission)
    {
        _documentsByAdmissionId.TryGetValue(admission.AdmissionId, out var documents);

        return new AdmissionApplication
        {
            Id = admission.AdmissionId,
            FullName = admission.ApplicantName,
            Email = admission.ApplicantEmail,
            Phone = admission.ApplicantPhone ?? "",
            Programme = admission.Programme?.ProgrammeName ?? "",
            Status = admission.Status,
            StudentId = admission.Student?.StudentNumber,
            SubmittedAt = admission.CreatedAt,
            Documents = documents ?? new List<AdmissionDocument>(),
        };
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
    public int ProgrammeId { get; set; }
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

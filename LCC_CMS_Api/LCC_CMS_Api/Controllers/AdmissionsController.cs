using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M1 — Admissions, Enrolment &amp; Readmission (Module Specification).
///
/// Phase 3: approve provisions an Entra member (@lccb.ac.pg), then creates
/// a <c>users</c> row (<c>entra_id</c> = Graph object id) and a matching
/// <c>students</c> row (<c>student_id</c> = <c>user_id</c>), then links
/// <c>admissions.student_id</c>. Graph failure leaves the admission Applied.
/// SQL failure after Graph deletes the Entra user. Welcome email is later
/// (FR-1.6). Reject still only updates status/date/reviewer.
/// Uploaded files remain on disk; document table rows wait on a later pass.
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

    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _fileStorage;
    private readonly IEntraUserProvisioner _entraUsers;
    private readonly ILogger<AdmissionsController> _logger;

    public AdmissionsController(
        LccCmsDbContext dbContext,
        ICurrentUser currentUser,
        IFileStorage fileStorage,
        IEntraUserProvisioner entraUsers,
        ILogger<AdmissionsController> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _entraUsers = entraUsers;
        _logger = logger;
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
    public async Task<ActionResult<AdmissionApplication>> Submit(
        [FromForm] AdmissionApplicationRequest request,
        CancellationToken cancellationToken)
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

            await using var input = file.OpenReadStream();
            var stored = await _fileStorage.SaveAsync(
                input,
                "admissions",
                ext,
                file.FileName,
                file.ContentType,
                cancellationToken);

            documents.Add(new AdmissionDocument
            {
                Type = type,
                Path = stored.StorageKey,
                FileName = stored.OriginalFileName,
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
    [HttpPatch("{id}/decision")]
    public async Task<ActionResult<AdmissionApplication>> Decide(
        int id,
        [FromBody] AdmissionDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Decision != "approve" && request.Decision != "reject")
        {
            return BadRequest("Decision must be 'approve' or 'reject'.");
        }

        var staff = await RequireStaffAsync(cancellationToken);
        if (staff.Error is not null) return staff.Error;

        string? entraObjectId = null;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var admission = await _dbContext.Admissions
                .Include(a => a.Programme)
                .Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.AdmissionId == id, cancellationToken);
            if (admission is null) return NotFound();

            if (!string.Equals(admission.Status, "Applied", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("This application has already been decided.");
            }

            admission.DecisionDate = DateOnly.FromDateTime(DateTime.UtcNow);
            admission.ReviewedBy = staff.StaffId;

            if (request.Decision == "reject")
            {
                admission.Status = "Rejected";
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Ok(ToApplication(admission));
            }

            if (!await _dbContext.Programmes.AnyAsync(p => p.ProgrammeId == admission.ProgrammeId, cancellationToken))
            {
                return BadRequest("Programme not found.");
            }

            var studentNumber = await NextStudentNumberAsync();
            if (await _dbContext.Students.AnyAsync(s => s.StudentNumber == studentNumber, cancellationToken))
            {
                return Conflict("Could not allocate a unique student number. Retry the decision.");
            }

            var mailNickname = studentNumber.Replace("-", "", StringComparison.Ordinal);
            var provisioned = await _entraUsers.CreateStudentAccountAsync(
                admission.ApplicantName,
                mailNickname,
                cancellationToken);
            entraObjectId = provisioned.ObjectId;

            if (await _dbContext.Users.AnyAsync(
                    u => u.Email == provisioned.UserPrincipalName || u.EntraId == provisioned.ObjectId,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                await CompensateEntraDeleteAsync(entraObjectId, cancellationToken);
                return Conflict("A user with this Entra account or email already exists.");
            }

            var user = new User
            {
                Email = provisioned.UserPrincipalName,
                Role = "Student",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                EntraId = provisioned.ObjectId,
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var student = new Student
            {
                StudentId = user.UserId,
                StudentNumber = studentNumber,
                ProgrammeId = admission.ProgrammeId,
                EnrolmentStatus = "Enrolled",
            };
            _dbContext.Students.Add(student);

            admission.StudentId = user.UserId;
            admission.Student = student;
            admission.Status = "Approved";

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            entraObjectId = null;

            return Ok(ToApplication(admission));
        }
        catch (EntraProvisioningException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            await CompensateEntraDeleteAsync(entraObjectId, cancellationToken);
            return StatusCode(ex.StatusCode, ex.Message);
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            await transaction.RollbackAsync(cancellationToken);
            await CompensateEntraDeleteAsync(entraObjectId, cancellationToken);
            return StatusCode(status, message);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await CompensateEntraDeleteAsync(entraObjectId, cancellationToken);
            throw;
        }
    }

    private async Task CompensateEntraDeleteAsync(string? objectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return;

        try
        {
            await _entraUsers.DeleteAccountAsync(objectId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not delete Entra user {ObjectId} after a failed approval.", objectId);
        }
    }

    private async Task<(int StaffId, ActionResult? Error)> RequireStaffAsync(
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            return (0, Unauthorized());
        }

        if (_currentUser.StaffId is not int staffId)
        {
            return (0, StatusCode(StatusCodes.Status403Forbidden));
        }

        return (staffId, null);
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

    private async Task<string> NextStudentNumberAsync()
    {
        var numbers = await _dbContext.Students
            .AsNoTracking()
            .Select(s => s.StudentNumber)
            .ToListAsync();

        var max = 24000;
        foreach (var number in numbers)
        {
            if (number.StartsWith("LCC-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(number.AsSpan(4), out var n)
                && n > max)
            {
                max = n;
            }
        }

        return $"LCC-{max + 1}";
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the admission decision.";

        if (ex.InnerException is not SqlException sql)
        {
            return false;
        }

        if (sql.Number is 2601 or 2627)
        {
            status = StatusCodes.Status409Conflict;
            var detail = sql.Message;
            if (detail.Contains("email", StringComparison.OrdinalIgnoreCase))
            {
                message = "A user with this email already exists.";
            }
            else if (detail.Contains("student_number", StringComparison.OrdinalIgnoreCase)
                     || detail.Contains("students", StringComparison.OrdinalIgnoreCase))
            {
                message = "Could not allocate a unique student number. Retry the decision.";
            }
            else if (detail.Contains("entra", StringComparison.OrdinalIgnoreCase))
            {
                message = "Could not allocate a unique account identifier. Retry the decision.";
            }
            else
            {
                message = "This decision conflicts with an existing record.";
            }

            return true;
        }

        if (sql.Number == 547)
        {
            status = StatusCodes.Status400BadRequest;
            message = "A related record was not found (programme, user, or student).";
            return true;
        }

        return false;
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

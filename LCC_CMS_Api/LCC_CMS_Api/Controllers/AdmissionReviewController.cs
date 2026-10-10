using System.Net.Http.Headers;
using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

[ApiController]
[Route("api/admissions")]
[Authorize(Policy = "RegistrarAdminOnly")]
public class AdmissionReviewController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _fileStorage;
    private readonly IAdmissionEmailSender _email;
    private readonly ILogger<AdmissionReviewController> _logger;

    public AdmissionReviewController(
        LccCmsDbContext dbContext,
        ICurrentUser currentUser,
        IFileStorage fileStorage,
        IAdmissionEmailSender email,
        ILogger<AdmissionReviewController> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _email = email;
        _logger = logger;
    }

    [HttpGet("{admissionId:int}/review")]
    public async Task<ActionResult<AdmissionReviewDetail>> GetReview(int admissionId, CancellationToken cancellationToken)
    {
        if (!await IsRegistrarAsync(cancellationToken)) return Forbid();
        var admission = await LoadAdmissionAsync(admissionId, cancellationToken);
        if (admission is null) return NotFound();
        var names = await ReviewerNamesAsync(admission, cancellationToken);
        var latest = await LatestEmailAsync(admissionId, cancellationToken);
        return Ok(ToDetail(admission, names, latest));
    }

    [HttpGet("{admissionId:int}/documents/{documentId:int}/content")]
    public async Task<IActionResult> OpenDocument(
        int admissionId,
        int documentId,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        if (!await IsRegistrarAsync(cancellationToken)) return Forbid();
        var document = await _dbContext.AdmissionDocuments
            .FirstOrDefaultAsync(
                d => d.AdmissionDocumentId == documentId && d.AdmissionId == admissionId,
                cancellationToken);
        if (document is null) return NotFound();

        Stream content;
        try
        {
            content = await _fileStorage.OpenReadAsync(document.StorageKey, cancellationToken);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or ArgumentException)
        {
            return NotFound();
        }

        if (document.OpenedAt is null)
        {
            document.OpenedAt = DateTime.UtcNow;
            _dbContext.AuditLogs.Add(Audit(
                _currentUser.UserId!.Value,
                "admission_documents",
                document.AdmissionDocumentId.ToString(),
                "opened=",
                "opened=yes"));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var mime = MimeFor(document);
        Response.Headers.CacheControl = "private, no-store";
        var disposition = new ContentDispositionHeaderValue(download ? "attachment" : "inline")
        {
            FileNameStar = document.OriginalFileName,
        };
        Response.Headers.ContentDisposition = disposition.ToString();
        return File(content, mime, enableRangeProcessing: true);
    }

    [HttpPut("{admissionId:int}/documents/{documentId:int}/review")]
    public async Task<ActionResult<AdmissionReviewDetail>> SaveDocumentReview(
        int admissionId,
        int documentId,
        [FromBody] DocumentReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsRegistrarAsync(cancellationToken)) return Forbid();
        if (!AdmissionReviewPolicy.IsOutcome(request.Outcome))
        {
            return BadRequest("Choose a document review outcome.");
        }

        var remark = (request.Remark ?? "").Trim();
        if (remark.Length > 1000) return BadRequest("The document remark must be 1000 characters or fewer.");

        var document = await _dbContext.AdmissionDocuments
            .FirstOrDefaultAsync(
                d => d.AdmissionDocumentId == documentId && d.AdmissionId == admissionId,
                cancellationToken);
        if (document is null) return NotFound();

        var previous = document.ReviewOutcome ?? "";
        document.ReviewOutcome = request.Outcome;
        document.ReviewRemark = remark.Length == 0 ? null : remark;
        document.ReviewedBy = _currentUser.UserId;
        document.ReviewedAt = DateTime.UtcNow;
        document.OpenedAt ??= document.ReviewedAt;
        _dbContext.AuditLogs.Add(Audit(
            _currentUser.UserId!.Value,
            "admission_documents",
            document.AdmissionDocumentId.ToString(),
            $"outcome={previous}",
            $"outcome={document.ReviewOutcome};remark={document.ReviewRemark}"));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetReview(admissionId, cancellationToken);
    }

    [HttpGet("{admissionId:int}/email-preview")]
    public async Task<ActionResult<AdmissionEmailPreview>> PreviewEmail(
        int admissionId,
        [FromQuery] string decision,
        [FromQuery] string? remark,
        CancellationToken cancellationToken)
    {
        if (!await IsRegistrarAsync(cancellationToken)) return Forbid();
        if (!AdmissionReviewPolicy.CanEmail(decision))
        {
            return BadRequest("Choose a final decision that has an applicant email.");
        }

        var admission = await LoadAdmissionAsync(admissionId, cancellationToken);
        if (admission is null) return NotFound();
        var text = string.IsNullOrWhiteSpace(remark)
            ? AdmissionReviewPolicy.DefaultRemark(decision)
            : remark.Trim();
        if (text.Length > 2000) return BadRequest("The decision remark must be 2000 characters or fewer.");
        var message = AdmissionEmailComposer.Compose(
            decision,
            admission.ApplicantName,
            admission.Programme?.ProgrammeName ?? "",
            ReferenceFor(admission.AdmissionId),
            text);
        return Ok(new AdmissionEmailPreview
        {
            To = admission.ApplicantEmail,
            Subject = message.Subject,
            Body = message.Body,
            Decision = decision,
            Remark = text,
        });
    }

    [HttpPost("{admissionId:int}/selection-decision")]
    public async Task<ActionResult<AdmissionReviewDetail>> SaveDecision(
        int admissionId,
        [FromBody] SelectionDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsRegistrarAsync(cancellationToken)) return Forbid();
        var admission = await LoadAdmissionAsync(admissionId, cancellationToken);
        if (admission is null) return NotFound();

        var saved = await RecordDecisionAsync(admission, request, cancellationToken);
        if (saved is not null) return saved;

        if (!request.SendEmail)
        {
            return await GetReview(admissionId, cancellationToken);
        }

        await SendAndLogAsync(admission, cancellationToken);
        return await GetReview(admissionId, cancellationToken);
    }

    [HttpPost("{admissionId:int}/emails/{emailLogId:int}/retry")]
    public async Task<ActionResult<AdmissionReviewDetail>> RetryEmail(
        int admissionId,
        int emailLogId,
        [FromBody] EmailRetryRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsRegistrarAsync(cancellationToken)) return Forbid();
        if (!request.Confirm)
        {
            return BadRequest("Confirm the resend before trying again.");
        }

        var admission = await LoadAdmissionAsync(admissionId, cancellationToken);
        if (admission is null) return NotFound();
        var existing = await _dbContext.AdmissionEmailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.AdmissionEmailLogId == emailLogId && row.AdmissionId == admissionId,
                cancellationToken);
        if (existing is null) return NotFound();
        if (!string.Equals(existing.DeliveryStatus, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("Only a failed email can be sent again.");
        }

        await SendAndLogAsync(admission, cancellationToken);
        return await GetReview(admissionId, cancellationToken);
    }

    private async Task<ActionResult?> RecordDecisionAsync(
        Admission admission,
        SelectionDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!AdmissionReviewPolicy.IsDecision(request.Decision))
        {
            return BadRequest("Select the final application decision.");
        }

        if (request.SendEmail && !AdmissionReviewPolicy.CanEmail(request.Decision))
        {
            return BadRequest("This decision does not have an applicant email.");
        }

        var remark = (request.Remark ?? "").Trim();
        if (request.Decision != AdmissionReviewPolicy.PendingCommittee && remark.Length == 0)
        {
            remark = AdmissionReviewPolicy.DefaultRemark(request.Decision);
        }
        if (remark.Length > 2000)
        {
            return BadRequest("The decision remark must be 2000 characters or fewer.");
        }

        var overrideReason = (request.OverrideReason ?? "").Trim();
        var addressed = admission.AdmissionDocuments.All(doc =>
            AdmissionReviewPolicy.DocumentAddressed(doc.OpenedAt, doc.ReviewOutcome));
        if (!addressed)
        {
            if (overrideReason.Length < 10)
            {
                return Conflict("Open or review every submitted document before recording the decision. An override requires a reason of at least 10 characters.");
            }
            if (overrideReason.Length > 1000)
            {
                return BadRequest("The override reason must be 1000 characters or fewer.");
            }
        }

        var previous = admission.SelectionDecision ?? "";
        admission.SelectionDecision = request.Decision;
        admission.DecisionRemark = remark.Length == 0 ? null : remark;
        admission.DecisionOverrideReason = addressed ? null : overrideReason;
        admission.DecisionRecordedBy = _currentUser.UserId;
        admission.DecisionRecordedAt = DateTime.UtcNow;
        _dbContext.AuditLogs.Add(Audit(
            _currentUser.UserId!.Value,
            "admissions",
            admission.AdmissionId.ToString(),
            $"decision={previous}",
            $"decision={admission.SelectionDecision};remark={admission.DecisionRemark};override={admission.DecisionOverrideReason}"));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task SendAndLogAsync(Admission admission, CancellationToken cancellationToken)
    {
        if (!AdmissionReviewPolicy.CanEmail(admission.SelectionDecision))
        {
            return;
        }

        var message = AdmissionEmailComposer.Compose(
            admission.SelectionDecision!,
            admission.ApplicantName,
            admission.Programme?.ProgrammeName ?? "",
            ReferenceFor(admission.AdmissionId),
            admission.DecisionRemark ?? "");
        var log = new AdmissionEmailLog
        {
            AdmissionId = admission.AdmissionId,
            Decision = admission.SelectionDecision!,
            RecipientEmail = admission.ApplicantEmail,
            Subject = message.Subject,
            DeliveryStatus = "NotSent",
            InitiatedBy = _currentUser.UserId!.Value,
            AttemptedAt = DateTime.UtcNow,
        };
        _dbContext.AdmissionEmailLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await _email.SendAsync(
            admission.ApplicantEmail,
            message.Subject,
            message.Body,
            cancellationToken);
        log.DeliveryStatus = result.Sent ? "Sent" : "Failed";
        log.SentAt = result.Sent ? DateTime.UtcNow : null;
        log.FailureMessage = result.Sent ? null : result.Error;
        _dbContext.AuditLogs.Add(Audit(
            _currentUser.UserId!.Value,
            "admission_email_log",
            log.AdmissionEmailLogId.ToString(),
            "status=NotSent",
            $"status={log.DeliveryStatus};to={log.RecipientEmail}"));
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (!result.Sent)
        {
            _logger.LogWarning(
                "Admission email was not delivered. AdmissionId={AdmissionId} Status={Status}",
                admission.AdmissionId,
                log.DeliveryStatus);
        }
    }

    private async Task<bool> IsRegistrarAsync(CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            return false;
        }

        return string.Equals(
            RoleNames.ToPolicyRole(_currentUser.Role),
            RoleNames.RegistrarAdmin,
            StringComparison.OrdinalIgnoreCase);
    }

    private Task<Admission?> LoadAdmissionAsync(int admissionId, CancellationToken cancellationToken)
    {
        return _dbContext.Admissions
            .Include(a => a.Programme)
            .Include(a => a.Student)
            .Include(a => a.AdmissionDocuments)
            .FirstOrDefaultAsync(a => a.AdmissionId == admissionId, cancellationToken);
    }

    private async Task<Dictionary<int, string>> ReviewerNamesAsync(Admission admission, CancellationToken cancellationToken)
    {
        var ids = admission.AdmissionDocuments
            .Where(d => d.ReviewedBy is not null)
            .Select(d => d.ReviewedBy!.Value)
            .ToList();
        if (admission.DecisionRecordedBy is int recorder) ids.Add(recorder);
        ids = ids.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();
        return await _dbContext.Staff
            .AsNoTracking()
            .Where(s => ids.Contains(s.StaffId))
            .ToDictionaryAsync(s => s.StaffId, s => s.FullName, cancellationToken);
    }

    private async Task<AdmissionEmailLog?> LatestEmailAsync(int admissionId, CancellationToken cancellationToken)
    {
        return await _dbContext.AdmissionEmailLogs
            .AsNoTracking()
            .Where(row => row.AdmissionId == admissionId)
            .OrderByDescending(row => row.AdmissionEmailLogId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static AdmissionReviewDetail ToDetail(
        Admission admission,
        IReadOnlyDictionary<int, string> names,
        AdmissionEmailLog? email)
    {
        var documents = admission.AdmissionDocuments
            .OrderBy(d => d.AdmissionDocumentId)
            .Select(d => new AdmissionDocumentReviewItem
            {
                DocumentId = d.AdmissionDocumentId,
                DocumentType = d.DocumentType,
                FileName = d.OriginalFileName,
                ContentType = MimeFor(d),
                UploadedAt = d.UploadedAt,
                OpenedAt = d.OpenedAt,
                ReviewOutcome = d.ReviewOutcome,
                ReviewOutcomeLabel = AdmissionReviewPolicy.OutcomeLabel(d.ReviewOutcome),
                ReviewRemark = d.ReviewRemark,
                ReviewedBy = d.ReviewedBy is int id && names.TryGetValue(id, out var name) ? name : null,
                ReviewedAt = d.ReviewedAt,
            })
            .ToList();
        var warnings = AdmissionReviewPolicy.Warnings(
            admission.AdmissionDocuments.Select(d => (d.ReviewOutcome, d.OpenedAt)));
        return new AdmissionReviewDetail
        {
            AdmissionId = admission.AdmissionId,
            ApplicantName = admission.ApplicantName,
            Email = admission.ApplicantEmail,
            Phone = admission.ApplicantPhone ?? "",
            Programme = admission.Programme?.ProgrammeName ?? "",
            Status = admission.Status,
            StudentNumber = admission.Student?.StudentNumber,
            SelectionDecision = admission.SelectionDecision,
            SelectionDecisionLabel = AdmissionReviewPolicy.DecisionLabel(admission.SelectionDecision),
            DecisionRemark = admission.DecisionRemark,
            DecisionOverrideReason = admission.DecisionOverrideReason,
            DecisionRecordedAt = admission.DecisionRecordedAt,
            ApplicationReference = ReferenceFor(admission.AdmissionId),
            Warnings = warnings.ToList(),
            AllDocumentsAddressed = admission.AdmissionDocuments.All(d =>
                AdmissionReviewPolicy.DocumentAddressed(d.OpenedAt, d.ReviewOutcome)),
            Documents = documents,
            EmailStatus = EmailStatusLabel(email?.DeliveryStatus),
            EmailFailure = email?.FailureMessage,
            EmailLogId = email?.AdmissionEmailLogId,
            CanRetryEmail = string.Equals(email?.DeliveryStatus, "Failed", StringComparison.OrdinalIgnoreCase),
            CanProceedToApproval = string.Equals(admission.Status, "Applied", StringComparison.OrdinalIgnoreCase)
                && admission.StudentId is null
                && AdmissionReviewPolicy.CanProceedToApproval(admission.SelectionDecision),
            OriginalVerificationOutstanding = admission.SelectionDecision == AdmissionReviewPolicy.PendingOriginals,
            ConsistencyWarnings = AdmissionReviewPolicy.ConsistencyWarnings(
                admission.Status,
                admission.SelectionDecision,
                admission.StudentId is not null,
                admission.DecisionRemark).ToList(),
        };
    }

    private static string EmailStatusLabel(string? status) => status switch
    {
        "Sent" => "Sent",
        "Failed" => "Failed",
        "NotSent" => "Not sent",
        _ => "Not sent",
    };

    private static string ReferenceFor(int admissionId) => $"LCCB-{admissionId:D5}";

    private static string MimeFor(Models.AdmissionDocument document)
    {
        var extension = Path.GetExtension(document.OriginalFileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => string.IsNullOrWhiteSpace(document.ContentType)
                ? "application/octet-stream"
                : document.ContentType,
        };
    }

    private static AuditLog Audit(int userId, string table, string recordId, string oldValue, string newValue) =>
        new()
        {
            UserId = userId,
            Action = "Update",
            TableName = table,
            RecordId = recordId,
            OldValue = oldValue,
            NewValue = newValue,
            Timestamp = DateTime.UtcNow,
        };
}

public class DocumentReviewRequest
{
    public string Outcome { get; set; } = "";
    public string? Remark { get; set; }
}

public class SelectionDecisionRequest
{
    public string Decision { get; set; } = "";
    public string? Remark { get; set; }
    public string? OverrideReason { get; set; }
    public bool SendEmail { get; set; }
}

public class EmailRetryRequest
{
    public bool Confirm { get; set; }
}

public class AdmissionEmailPreview
{
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Decision { get; set; } = "";
    public string Remark { get; set; } = "";
}

public class AdmissionDocumentReviewItem
{
    public int DocumentId { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public string? ReviewOutcome { get; set; }
    public string ReviewOutcomeLabel { get; set; } = "";
    public string? ReviewRemark { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class AdmissionReviewDetail
{
    public int AdmissionId { get; set; }
    public string ApplicantName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Programme { get; set; } = "";
    public string Status { get; set; } = "";
    public string? StudentNumber { get; set; }
    public string? SelectionDecision { get; set; }
    public string? SelectionDecisionLabel { get; set; }
    public string? DecisionRemark { get; set; }
    public string? DecisionOverrideReason { get; set; }
    public DateTime? DecisionRecordedAt { get; set; }
    public string ApplicationReference { get; set; } = "";
    public List<string> Warnings { get; set; } = new();
    public bool AllDocumentsAddressed { get; set; }
    public List<AdmissionDocumentReviewItem> Documents { get; set; } = new();
    public string EmailStatus { get; set; } = "Not sent";
    public string? EmailFailure { get; set; }
    public int? EmailLogId { get; set; }
    public bool CanRetryEmail { get; set; }
    public bool CanProceedToApproval { get; set; }
    public bool OriginalVerificationOutstanding { get; set; }
    public List<string> ConsistencyWarnings { get; set; } = new();
}

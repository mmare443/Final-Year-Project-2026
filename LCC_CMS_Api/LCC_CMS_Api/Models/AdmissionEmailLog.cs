namespace LCC_CMS_Api.Models;

public class AdmissionEmailLog
{
    public int AdmissionEmailLogId { get; set; }

    public int AdmissionId { get; set; }

    public string Decision { get; set; } = "";

    public string RecipientEmail { get; set; } = "";

    public string Subject { get; set; } = "";

    public string DeliveryStatus { get; set; } = "";

    public DateTime? AttemptedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public string? FailureMessage { get; set; }

    public int InitiatedBy { get; set; }
}

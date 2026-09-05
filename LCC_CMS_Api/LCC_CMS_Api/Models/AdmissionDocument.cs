namespace LCC_CMS_Api.Models;

public partial class AdmissionDocument
{
    public int AdmissionDocumentId { get; set; }

    public int AdmissionId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual Admission Admission { get; set; } = null!;
}

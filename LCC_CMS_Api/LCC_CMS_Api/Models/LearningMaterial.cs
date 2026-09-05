namespace LCC_CMS_Api.Models;

public partial class LearningMaterial
{
    public int LearningMaterialId { get; set; }

    public int AllocationId { get; set; }

    public string Title { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; }

    public int? UploadedByStaffId { get; set; }

    public virtual CourseAllocation Allocation { get; set; } = null!;

    public virtual Staff? UploadedByStaff { get; set; }
}

using System;

namespace LCC_CMS_Api.Models;

public partial class Announcement
{
    public int AnnouncementId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string Audience { get; set; } = "EVERYONE";

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Priority { get; set; } = "Normal";

    public bool IsPublished { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Staff CreatedByNavigation { get; set; } = null!;
}

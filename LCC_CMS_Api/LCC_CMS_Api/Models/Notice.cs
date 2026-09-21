using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Notice
{
    public int NoticeId { get; set; }

    public int AuthorId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? TargetRole { get; set; }

    public string Audience { get; set; } = "Everyone";

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Priority { get; set; } = "Normal";

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public DateTime PostedAt { get; set; }

    public virtual Staff Author { get; set; } = null!;
}

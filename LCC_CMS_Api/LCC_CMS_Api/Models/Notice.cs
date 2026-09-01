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

    public DateTime PostedAt { get; set; }

    public virtual Staff Author { get; set; } = null!;
}

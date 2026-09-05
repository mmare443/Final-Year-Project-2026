using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Document
{
    public int DocumentId { get; set; }

    public int StudentId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public string? ContentType { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual Student Student { get; set; } = null!;
}

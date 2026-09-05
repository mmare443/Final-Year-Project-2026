using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Submission
{
    public int SubmissionId { get; set; }

    public int AssignmentId { get; set; }

    public int StudentId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public string? ContentType { get; set; }

    public DateTime SubmittedAt { get; set; }

    public bool IsLate { get; set; }

    public decimal? MarksAwarded { get; set; }

    public string? Feedback { get; set; }

    public virtual Assignment Assignment { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}

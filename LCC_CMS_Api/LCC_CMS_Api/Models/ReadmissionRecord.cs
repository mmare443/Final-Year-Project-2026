using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class ReadmissionRecord
{
    public int ReadmissionId { get; set; }

    public int StudentId { get; set; }

    public int RequestedSemesterId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int? ReviewedBy { get; set; }

    public DateOnly? DecisionDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Semester RequestedSemester { get; set; } = null!;

    public virtual Staff? ReviewedByNavigation { get; set; }

    public virtual Student Student { get; set; } = null!;
}

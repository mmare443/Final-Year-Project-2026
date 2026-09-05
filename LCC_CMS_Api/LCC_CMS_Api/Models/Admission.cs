using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Admission
{
    public int AdmissionId { get; set; }

    public int ProgrammeId { get; set; }

    public string ApplicantName { get; set; } = null!;

    public string ApplicantEmail { get; set; } = null!;

    public string? ApplicantPhone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string Status { get; set; } = null!;

    public int? ReviewedBy { get; set; }

    public DateOnly? DecisionDate { get; set; }

    public int? StudentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Programme Programme { get; set; } = null!;

    public virtual ICollection<AdmissionDocument> AdmissionDocuments { get; set; } = new List<AdmissionDocument>();

    public virtual Staff? ReviewedByNavigation { get; set; }

    public virtual Student? Student { get; set; }
}

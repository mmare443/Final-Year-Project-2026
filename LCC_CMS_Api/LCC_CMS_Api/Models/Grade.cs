using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Grade
{
    public int GradeId { get; set; }

    public int AssessmentId { get; set; }

    public int StudentId { get; set; }

    public decimal MarksObtained { get; set; }

    public string? GradeLetter { get; set; }

    public bool Published { get; set; }

    public int? OverriddenBy { get; set; }

    public string? OverrideJustification { get; set; }

    public virtual Assessment Assessment { get; set; } = null!;

    public virtual Staff? OverriddenByNavigation { get; set; }

    public virtual Student Student { get; set; } = null!;
}

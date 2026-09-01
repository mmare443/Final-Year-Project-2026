using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class WelfareCase
{
    public int CaseId { get; set; }

    public int StudentId { get; set; }

    public int AssignedOfficerId { get; set; }

    public string CaseType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime DateLogged { get; set; }

    public DateTime? DateResolved { get; set; }

    public virtual Staff AssignedOfficer { get; set; } = null!;

    public virtual ICollection<CaseNote> CaseNotes { get; set; } = new List<CaseNote>();

    public virtual Student Student { get; set; } = null!;
}

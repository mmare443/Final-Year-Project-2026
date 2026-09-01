using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class CaseNote
{
    public int CaseNoteId { get; set; }

    public int CaseId { get; set; }

    public int StaffId { get; set; }

    public string Note { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual WelfareCase Case { get; set; } = null!;

    public virtual Staff Staff { get; set; } = null!;
}

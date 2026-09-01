using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Registration
{
    public int RegistrationId { get; set; }

    public int StudentId { get; set; }

    public int AllocationId { get; set; }

    public int AttemptNo { get; set; }

    public string Status { get; set; } = null!;

    public int? ApprovedBy { get; set; }

    public DateTime RegisteredAt { get; set; }

    public virtual CourseAllocation Allocation { get; set; } = null!;

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Student Student { get; set; } = null!;
}

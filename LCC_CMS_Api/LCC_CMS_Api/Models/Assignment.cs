using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Assignment
{
    public int AssignmentId { get; set; }

    public int AllocationId { get; set; }

    public string Title { get; set; } = null!;

    public string? Instructions { get; set; }

    public DateTime DueDate { get; set; }

    public decimal MaxMarks { get; set; }

    public virtual CourseAllocation Allocation { get; set; } = null!;

    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}

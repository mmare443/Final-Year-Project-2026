using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Assessment
{
    public int AssessmentId { get; set; }

    public int AllocationId { get; set; }

    public string Title { get; set; } = null!;

    public decimal WeightPercent { get; set; }

    public decimal MaxMarks { get; set; }

    public virtual CourseAllocation Allocation { get; set; } = null!;

    public virtual ICollection<Grade> Grades { get; set; } = new List<Grade>();
}

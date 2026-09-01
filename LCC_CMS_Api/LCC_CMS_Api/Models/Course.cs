using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Course
{
    public int CourseId { get; set; }

    public int ProgrammeId { get; set; }

    public string CourseCode { get; set; } = null!;

    public string CourseName { get; set; } = null!;

    public decimal CreditValue { get; set; }

    public byte YearLevel { get; set; }

    public byte SemesterNo { get; set; }

    public bool IsCore { get; set; }

    public int? PrerequisiteCourseId { get; set; }

    public virtual ICollection<CourseAllocation> CourseAllocations { get; set; } = new List<CourseAllocation>();

    public virtual ICollection<Course> InversePrerequisiteCourse { get; set; } = new List<Course>();

    public virtual Course? PrerequisiteCourse { get; set; }

    public virtual Programme Programme { get; set; } = null!;
}

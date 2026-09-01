using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Semester
{
    public int SemesterId { get; set; }

    public int AcademicYearId { get; set; }

    public string SemesterName { get; set; } = null!;

    public byte SemesterNo { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    public virtual AcademicYear AcademicYear { get; set; } = null!;

    public virtual ICollection<CourseAllocation> CourseAllocations { get; set; } = new List<CourseAllocation>();

    public virtual ICollection<ReadmissionRecord> ReadmissionRecords { get; set; } = new List<ReadmissionRecord>();
}

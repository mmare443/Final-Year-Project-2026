using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Programme
{
    public int ProgrammeId { get; set; }

    public int DepartmentId { get; set; }

    public string ProgrammeName { get; set; } = null!;

    public decimal DurationYears { get; set; }

    public virtual ICollection<Admission> Admissions { get; set; } = new List<Admission>();

    public virtual ICollection<Course> Courses { get; set; } = new List<Course>();

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}

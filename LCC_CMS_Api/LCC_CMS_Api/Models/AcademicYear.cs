using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class AcademicYear
{
    public int AcademicYearId { get; set; }

    public string YearName { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public virtual ICollection<Semester> Semesters { get; set; } = new List<Semester>();
}

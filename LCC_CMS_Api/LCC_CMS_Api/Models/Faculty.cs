using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Faculty
{
    public int FacultyId { get; set; }

    public string FacultyName { get; set; } = null!;

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();
}

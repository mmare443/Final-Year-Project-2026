using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Department
{
    public int DepartmentId { get; set; }

    public int FacultyId { get; set; }

    public string DepartmentName { get; set; } = null!;

    public virtual Faculty Faculty { get; set; } = null!;

    public virtual ICollection<Programme> Programmes { get; set; } = new List<Programme>();

    public virtual ICollection<Staff> Staff { get; set; } = new List<Staff>();
}

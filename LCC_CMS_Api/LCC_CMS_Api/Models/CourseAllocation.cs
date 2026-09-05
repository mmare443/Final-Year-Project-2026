using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class CourseAllocation
{
    public int AllocationId { get; set; }

    public int CourseId { get; set; }

    public int StaffId { get; set; }

    public int SemesterId { get; set; }

    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual ICollection<AttendanceSession> AttendanceSessions { get; set; } = new List<AttendanceSession>();

    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<LearningMaterial> LearningMaterials { get; set; } = new List<LearningMaterial>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual Semester Semester { get; set; } = null!;

    public virtual Staff Staff { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class AttendanceSession
{
    public int SessionId { get; set; }

    public int AllocationId { get; set; }

    public DateOnly SessionDate { get; set; }

    public virtual CourseAllocation Allocation { get; set; } = null!;

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

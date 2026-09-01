using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Attendance
{
    public int AttendanceId { get; set; }

    public int SessionId { get; set; }

    public int StudentId { get; set; }

    public string Status { get; set; } = null!;

    public virtual AttendanceSession Session { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}

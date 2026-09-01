using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Student
{
    public int StudentId { get; set; }

    public string StudentNumber { get; set; } = null!;

    public int ProgrammeId { get; set; }

    public string EnrolmentStatus { get; set; } = null!;

    public string? EmergencyContact { get; set; }

    public virtual AccommodationRecord? AccommodationRecord { get; set; }

    public virtual Admission? Admission { get; set; }

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<Grade> Grades { get; set; } = new List<Grade>();

    public virtual Programme Programme { get; set; } = null!;

    public virtual ICollection<ReadmissionRecord> ReadmissionRecords { get; set; } = new List<ReadmissionRecord>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual User StudentNavigation { get; set; } = null!;

    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();

    public virtual ICollection<WelfareCase> WelfareCases { get; set; } = new List<WelfareCase>();
}

using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Staff
{
    public int StaffId { get; set; }

    public string StaffNumber { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public int DepartmentId { get; set; }

    public string JobTitle { get; set; } = null!;

    public string? EmploymentDetails { get; set; }

    public string? PhoneNumber { get; set; }

    public string? PersonalEmail { get; set; }

    public string? PostalAddress { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public string? Village { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? EmergencyRelationship { get; set; }

    public virtual ICollection<AccommodationRecord> AccommodationRecords { get; set; } = new List<AccommodationRecord>();

    public virtual ICollection<Admission> Admissions { get; set; } = new List<Admission>();

    public virtual ICollection<LearningMaterial> UploadedLearningMaterials { get; set; } = new List<LearningMaterial>();

    public virtual ICollection<CaseNote> CaseNotes { get; set; } = new List<CaseNote>();

    public virtual ICollection<CourseAllocation> CourseAllocations { get; set; } = new List<CourseAllocation>();

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<Grade> Grades { get; set; } = new List<Grade>();

    public virtual ICollection<Notice> Notices { get; set; } = new List<Notice>();

    public virtual ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();

    public virtual ICollection<ReadmissionRecord> ReadmissionRecords { get; set; } = new List<ReadmissionRecord>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual User StaffNavigation { get; set; } = null!;

    public virtual ICollection<WelfareCase> WelfareCases { get; set; } = new List<WelfareCase>();
}

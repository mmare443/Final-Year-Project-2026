using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Models;

public partial class LccCmsDbContext : DbContext
{
    public LccCmsDbContext()
    {
    }

    public LccCmsDbContext(DbContextOptions<LccCmsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AcademicYear> AcademicYears { get; set; }

    public virtual DbSet<AdmissionDocument> AdmissionDocuments { get; set; }

    public virtual DbSet<AccommodationRecord> AccommodationRecords { get; set; }

    public virtual DbSet<Admission> Admissions { get; set; }

    public virtual DbSet<Assessment> Assessments { get; set; }

    public virtual DbSet<Assignment> Assignments { get; set; }

    public virtual DbSet<Attendance> Attendances { get; set; }

    public virtual DbSet<AttendanceSession> AttendanceSessions { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<CaseNote> CaseNotes { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<CourseAllocation> CourseAllocations { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<Faculty> Faculties { get; set; }

    public virtual DbSet<Grade> Grades { get; set; }

    public virtual DbSet<GradeScale> GradeScales { get; set; }

    public virtual DbSet<Hostel> Hostels { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Notice> Notices { get; set; }

    public virtual DbSet<Programme> Programmes { get; set; }

    public virtual DbSet<ReadmissionRecord> ReadmissionRecords { get; set; }

    public virtual DbSet<Registration> Registrations { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<Semester> Semesters { get; set; }

    public virtual DbSet<Staff> Staff { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<Submission> Submissions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<WelfareCase> WelfareCases { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost;Database=LCCCMSDB;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AcademicYear>(entity =>
        {
            entity.HasKey(e => e.AcademicYearId).HasName("PK__academic__11CFB97455E9C146");

            entity.ToTable("academic_years");

            entity.HasIndex(e => e.YearName, "UQ__academic__252258BEA06C767B").IsUnique();

            entity.Property(e => e.AcademicYearId).HasColumnName("academic_year_id");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.YearName)
                .HasMaxLength(9)
                .HasColumnName("year_name");
        });

        modelBuilder.Entity<AccommodationRecord>(entity =>
        {
            entity.HasKey(e => e.AccommodationId).HasName("PK__accommod__004EC325E01B3959");

            entity.ToTable("accommodation_records");

            entity.HasIndex(e => e.RoomId, "IX_accommodation_room");

            entity.HasIndex(e => e.StudentId, "UX_accommodation_one_active")
                .IsUnique()
                .HasFilter("([status]='Active')");

            entity.Property(e => e.AccommodationId).HasColumnName("accommodation_id");
            entity.Property(e => e.AllocatedBy).HasColumnName("allocated_by");
            entity.Property(e => e.DateAllocated)
                .HasDefaultValueSql("(CONVERT([date],sysutcdatetime()))")
                .HasColumnName("date_allocated");
            entity.Property(e => e.DateVacated).HasColumnName("date_vacated");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasDefaultValue("Active")
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.AllocatedByNavigation).WithMany(p => p.AccommodationRecords)
                .HasForeignKey(d => d.AllocatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__accommoda__alloc__3587F3E0");

            entity.HasOne(d => d.Room).WithMany(p => p.AccommodationRecords)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__accommoda__room___32AB8735");

            entity.HasOne(d => d.Student).WithOne(p => p.AccommodationRecord)
                .HasForeignKey<AccommodationRecord>(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__accommoda__stude__31B762FC");
        });

        modelBuilder.Entity<Admission>(entity =>
        {
            entity.HasKey(e => e.AdmissionId).HasName("PK__admissio__3D9F8C7222C1BCC5");

            entity.ToTable("admissions");

            entity.HasIndex(e => e.ProgrammeId, "IX_admissions_programme");

            entity.HasIndex(e => e.Status, "IX_admissions_status");

            entity.HasIndex(e => e.StudentId, "UQ__admissio__2A33069BFE10F431").IsUnique();

            entity.Property(e => e.AdmissionId).HasColumnName("admission_id");
            entity.Property(e => e.ApplicantEmail)
                .HasMaxLength(255)
                .HasColumnName("applicant_email");
            entity.Property(e => e.ApplicantName)
                .HasMaxLength(150)
                .HasColumnName("applicant_name");
            entity.Property(e => e.ApplicantPhone)
                .HasMaxLength(30)
                .HasColumnName("applicant_phone");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
            entity.Property(e => e.DecisionDate).HasColumnName("decision_date");
            entity.Property(e => e.Gender)
                .HasMaxLength(20)
                .HasColumnName("gender");
            entity.Property(e => e.ProgrammeId).HasColumnName("programme_id");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Applied")
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Programme).WithMany(p => p.Admissions)
                .HasForeignKey(d => d.ProgrammeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__admission__progr__76969D2E");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.Admissions)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK__admission__revie__797309D9");

            entity.HasOne(d => d.Student).WithOne(p => p.Admission)
                .HasForeignKey<Admission>(d => d.StudentId)
                .HasConstraintName("FK__admission__stude__7A672E12");
        });

        modelBuilder.Entity<AdmissionDocument>(entity =>
        {
            entity.HasKey(e => e.AdmissionDocumentId);

            entity.ToTable("admission_documents");

            entity.HasIndex(e => e.AdmissionId, "IX_admission_documents_admission");

            entity.Property(e => e.AdmissionDocumentId)
                .HasColumnName("admission_document_id");
            entity.Property(e => e.AdmissionId)
                .HasColumnName("admission_id");
            entity.Property(e => e.DocumentType)
                .HasMaxLength(100)
                .HasColumnName("document_type");
            entity.Property(e => e.StorageKey)
                .HasMaxLength(500)
                .HasColumnName("storage_key");
            entity.Property(e => e.OriginalFileName)
                .HasMaxLength(255)
                .HasColumnName("original_file_name");
            entity.Property(e => e.ContentType)
                .HasMaxLength(255)
                .HasColumnName("content_type");
            entity.Property(e => e.FileSize)
                .HasColumnName("file_size");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Admission)
                .WithMany(p => p.AdmissionDocuments)
                .HasForeignKey(d => d.AdmissionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__admission_documents__admission");
        });

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.HasKey(e => e.AssessmentId).HasName("PK__assessme__00B98C266B15421B");

            entity.ToTable("assessments");

            entity.HasIndex(e => e.AllocationId, "IX_assessments_allocation");

            entity.Property(e => e.AssessmentId).HasColumnName("assessment_id");
            entity.Property(e => e.AllocationId).HasColumnName("allocation_id");
            entity.Property(e => e.MaxMarks)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("max_marks");
            entity.Property(e => e.Title)
                .HasMaxLength(150)
                .HasColumnName("title");
            entity.Property(e => e.WeightPercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("weight_percent");

            entity.HasOne(d => d.Allocation).WithMany(p => p.Assessments)
                .HasForeignKey(d => d.AllocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__assessmen__alloc__245D67DE");
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__assignme__DA891814FB266078");

            entity.ToTable("assignments");

            entity.HasIndex(e => e.AllocationId, "IX_assignments_allocation");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.AllocationId).HasColumnName("allocation_id");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.Instructions).HasColumnName("instructions");
            entity.Property(e => e.MaxMarks)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("max_marks");
            entity.Property(e => e.Title)
                .HasMaxLength(150)
                .HasColumnName("title");

            entity.HasOne(d => d.Allocation).WithMany(p => p.Assignments)
                .HasForeignKey(d => d.AllocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__assignmen__alloc__19DFD96B");
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.AttendanceId).HasName("PK__attendan__20D6A968A5542995");

            entity.ToTable("attendances");

            entity.HasIndex(e => e.Status, "IX_att_status");

            entity.HasIndex(e => e.StudentId, "IX_att_student");

            entity.HasIndex(e => new { e.SessionId, e.StudentId }, "UQ_attendances").IsUnique();

            entity.Property(e => e.AttendanceId).HasColumnName("attendance_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Session).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__attendanc__sessi__151B244E");

            entity.HasOne(d => d.Student).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__attendanc__stude__160F4887");
        });

        modelBuilder.Entity<AttendanceSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__attendan__69B13FDC5C6935C5");

            entity.ToTable("attendance_sessions");

            entity.HasIndex(e => e.AllocationId, "IX_sessions_allocation");

            entity.HasIndex(e => new { e.AllocationId, e.SessionDate }, "UQ_attendance_sessions").IsUnique();

            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.AllocationId).HasColumnName("allocation_id");
            entity.Property(e => e.SessionDate).HasColumnName("session_date");

            entity.HasOne(d => d.Allocation).WithMany(p => p.AttendanceSessions)
                .HasForeignKey(d => d.AllocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__attendanc__alloc__114A936A");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId).HasName("PK__audit_lo__5AF33E33FACE3A90");

            entity.ToTable("audit_logs");

            entity.HasIndex(e => new { e.TableName, e.RecordId }, "IX_audit_table_record");

            entity.HasIndex(e => e.Timestamp, "IX_audit_timestamp");

            entity.HasIndex(e => e.UserId, "IX_audit_user");

            entity.Property(e => e.AuditId).HasColumnName("audit_id");
            entity.Property(e => e.Action)
                .HasMaxLength(10)
                .HasColumnName("action");
            entity.Property(e => e.NewValue).HasColumnName("new_value");
            entity.Property(e => e.OldValue).HasColumnName("old_value");
            entity.Property(e => e.RecordId)
                .HasMaxLength(50)
                .HasColumnName("record_id");
            entity.Property(e => e.TableName)
                .HasMaxLength(100)
                .HasColumnName("table_name");
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("timestamp");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__audit_log__user___55F4C372");
        });

        modelBuilder.Entity<CaseNote>(entity =>
        {
            entity.HasKey(e => e.CaseNoteId).HasName("PK__case_not__5A1DFED2B05D9FCB");

            entity.ToTable("case_notes");

            entity.HasIndex(e => e.CaseId, "IX_case_notes_case");

            entity.Property(e => e.CaseNoteId).HasColumnName("case_note_id");
            entity.Property(e => e.CaseId).HasColumnName("case_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Note)
                .HasMaxLength(1000)
                .HasColumnName("note");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");

            entity.HasOne(d => d.Case).WithMany(p => p.CaseNotes)
                .HasForeignKey(d => d.CaseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__case_note__case___40058253");

            entity.HasOne(d => d.Staff).WithMany(p => p.CaseNotes)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__case_note__staff__40F9A68C");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("PK__courses__8F1EF7AE9F0086D2");

            entity.ToTable("courses");

            entity.HasIndex(e => e.PrerequisiteCourseId, "IX_courses_prerequisite");

            entity.HasIndex(e => e.ProgrammeId, "IX_courses_programme");

            entity.HasIndex(e => new { e.ProgrammeId, e.YearLevel, e.SemesterNo }, "IX_courses_year_semester");

            entity.HasIndex(e => new { e.ProgrammeId, e.CourseCode }, "UQ_courses_programme_code").IsUnique();

            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CourseCode)
                .HasMaxLength(20)
                .HasColumnName("course_code");
            entity.Property(e => e.CourseName)
                .HasMaxLength(150)
                .HasColumnName("course_name");
            entity.Property(e => e.CreditValue)
                .HasColumnType("decimal(4, 1)")
                .HasColumnName("credit_value");
            entity.Property(e => e.IsCore)
                .HasDefaultValue(true)
                .HasColumnName("is_core");
            entity.Property(e => e.PrerequisiteCourseId).HasColumnName("prerequisite_course_id");
            entity.Property(e => e.ProgrammeId).HasColumnName("programme_id");
            entity.Property(e => e.SemesterNo).HasColumnName("semester_no");
            entity.Property(e => e.YearLevel).HasColumnName("year_level");

            entity.HasOne(d => d.PrerequisiteCourse).WithMany(p => p.InversePrerequisiteCourse)
                .HasForeignKey(d => d.PrerequisiteCourseId)
                .HasConstraintName("FK__courses__prerequ__6D0D32F4");

            entity.HasOne(d => d.Programme).WithMany(p => p.Courses)
                .HasForeignKey(d => d.ProgrammeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__courses__program__68487DD7");
        });

        modelBuilder.Entity<CourseAllocation>(entity =>
        {
            entity.HasKey(e => e.AllocationId).HasName("PK__course_a__5DFAFF30DDE1949B");

            entity.ToTable("course_allocations");

            entity.HasIndex(e => e.CourseId, "IX_alloc_course");

            entity.HasIndex(e => e.SemesterId, "IX_alloc_semester");

            entity.HasIndex(e => e.StaffId, "IX_alloc_staff");

            entity.HasIndex(e => new { e.CourseId, e.StaffId, e.SemesterId }, "UQ_course_allocations").IsUnique();

            entity.Property(e => e.AllocationId).HasColumnName("allocation_id");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.SemesterId).HasColumnName("semester_id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");

            entity.HasOne(d => d.Course).WithMany(p => p.CourseAllocations)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__course_al__cours__70DDC3D8");

            entity.HasOne(d => d.Semester).WithMany(p => p.CourseAllocations)
                .HasForeignKey(d => d.SemesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__course_al__semes__72C60C4A");

            entity.HasOne(d => d.Staff).WithMany(p => p.CourseAllocations)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__course_al__staff__71D1E811");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__departme__C22324228D0E2D9E");

            entity.ToTable("departments");

            entity.HasIndex(e => e.FacultyId, "IX_departments_faculty");

            entity.HasIndex(e => new { e.FacultyId, e.DepartmentName }, "UQ_departments_name").IsUnique();

            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.DepartmentName)
                .HasMaxLength(150)
                .HasColumnName("department_name");
            entity.Property(e => e.FacultyId).HasColumnName("faculty_id");

            entity.HasOne(d => d.Faculty).WithMany(p => p.Departments)
                .HasForeignKey(d => d.FacultyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__departmen__facul__3B75D760");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("PK__document__9666E8AC36E3328D");

            entity.ToTable("documents");

            entity.HasIndex(e => e.StudentId, "IX_documents_student");

            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.DocumentType)
                .HasMaxLength(30)
                .HasColumnName("document_type");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .HasColumnName("file_url");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Student).WithMany(p => p.Documents)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__documents__stude__51300E55");
        });

        modelBuilder.Entity<Faculty>(entity =>
        {
            entity.HasKey(e => e.FacultyId).HasName("PK__facultie__7B00413CDDE2679E");

            entity.ToTable("faculties");

            entity.HasIndex(e => e.FacultyName, "UQ_faculties_name").IsUnique();

            entity.Property(e => e.FacultyId).HasColumnName("faculty_id");
            entity.Property(e => e.FacultyName)
                .HasMaxLength(150)
                .HasColumnName("faculty_name");
        });

        modelBuilder.Entity<Grade>(entity =>
        {
            entity.HasKey(e => e.GradeId).HasName("PK__grades__3A8F732C9F64AE84");

            entity.ToTable("grades");

            entity.HasIndex(e => e.Published, "IX_grades_published");

            entity.HasIndex(e => e.StudentId, "IX_grades_student");

            entity.HasIndex(e => new { e.AssessmentId, e.StudentId }, "UQ_grades").IsUnique();

            entity.Property(e => e.GradeId).HasColumnName("grade_id");
            entity.Property(e => e.AssessmentId).HasColumnName("assessment_id");
            entity.Property(e => e.GradeLetter)
                .HasMaxLength(3)
                .HasColumnName("grade_letter");
            entity.Property(e => e.MarksObtained)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("marks_obtained");
            entity.Property(e => e.OverriddenBy).HasColumnName("overridden_by");
            entity.Property(e => e.OverrideJustification)
                .HasMaxLength(500)
                .HasColumnName("override_justification");
            entity.Property(e => e.Published).HasColumnName("published");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Assessment).WithMany(p => p.Grades)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__grades__assessme__2A164134");

            entity.HasOne(d => d.OverriddenByNavigation).WithMany(p => p.Grades)
                .HasForeignKey(d => d.OverriddenBy)
                .HasConstraintName("FK__grades__overridd__2EDAF651");

            entity.HasOne(d => d.Student).WithMany(p => p.Grades)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__grades__student___2B0A656D");
        });

        modelBuilder.Entity<GradeScale>(entity =>
        {
            entity.HasKey(e => e.GradeLetter).HasName("PK__grade_sc__1B70202E85C20412");

            entity.ToTable("grade_scale");

            entity.Property(e => e.GradeLetter)
                .HasMaxLength(3)
                .HasColumnName("grade_letter");
            entity.Property(e => e.GradeValue)
                .HasColumnType("decimal(3, 1)")
                .HasColumnName("grade_value");
        });

        modelBuilder.Entity<Hostel>(entity =>
        {
            entity.HasKey(e => e.HostelId).HasName("PK__hostels__A3EE317ED1217440");

            entity.ToTable("hostels");

            entity.HasIndex(e => e.HostelName, "UQ__hostels__41ECEC760F433220").IsUnique();

            entity.Property(e => e.HostelId).HasColumnName("hostel_id");
            entity.Property(e => e.HostelName)
                .HasMaxLength(100)
                .HasColumnName("hostel_name");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__messages__0BBF6EE6F4610430");

            entity.ToTable("messages");

            entity.HasIndex(e => e.RecipientId, "IX_messages_recipient");

            entity.HasIndex(e => e.SenderId, "IX_messages_sender");

            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.Content)
                .HasMaxLength(2000)
                .HasColumnName("content");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.RecipientId).HasColumnName("recipient_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("sent_at");

            entity.HasOne(d => d.Recipient).WithMany(p => p.MessageRecipients)
                .HasForeignKey(d => d.RecipientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__messages__recipi__4A8310C6");

            entity.HasOne(d => d.Sender).WithMany(p => p.MessageSenders)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__messages__sender__498EEC8D");
        });

        modelBuilder.Entity<Notice>(entity =>
        {
            entity.HasKey(e => e.NoticeId).HasName("PK__notices__3E82A5DBCAFD2DA8");

            entity.ToTable("notices");

            entity.HasIndex(e => e.PostedAt, "IX_notices_posted_at");

            entity.Property(e => e.NoticeId).HasColumnName("notice_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.PostedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("posted_at");
            entity.Property(e => e.TargetRole)
                .HasMaxLength(30)
                .HasColumnName("target_role");
            entity.Property(e => e.Title)
                .HasMaxLength(150)
                .HasColumnName("title");

            entity.HasOne(d => d.Author).WithMany(p => p.Notices)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__notices__author___44CA3770");
        });

        modelBuilder.Entity<Programme>(entity =>
        {
            entity.HasKey(e => e.ProgrammeId).HasName("PK__programm__0D327BFA6F5212A2");

            entity.ToTable("programmes");

            entity.HasIndex(e => e.DepartmentId, "IX_programmes_department");

            entity.HasIndex(e => new { e.DepartmentId, e.ProgrammeName }, "UQ_programmes_name").IsUnique();

            entity.Property(e => e.ProgrammeId).HasColumnName("programme_id");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.DurationYears)
                .HasColumnType("decimal(3, 1)")
                .HasColumnName("duration_years");
            entity.Property(e => e.ProgrammeName)
                .HasMaxLength(150)
                .HasColumnName("programme_name");

            entity.HasOne(d => d.Department).WithMany(p => p.Programmes)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__programme__depar__3F466844");
        });

        modelBuilder.Entity<ReadmissionRecord>(entity =>
        {
            entity.HasKey(e => e.ReadmissionId).HasName("PK__readmiss__789142F7CB6D554E");

            entity.ToTable("readmission_records");

            entity.HasIndex(e => e.Status, "IX_readmission_status");

            entity.HasIndex(e => e.StudentId, "IX_readmission_student");

            entity.Property(e => e.ReadmissionId).HasColumnName("readmission_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DecisionDate).HasColumnName("decision_date");
            entity.Property(e => e.Reason)
                .HasMaxLength(500)
                .HasColumnName("reason");
            entity.Property(e => e.RequestedSemesterId).HasColumnName("requested_semester_id");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending")
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.RequestedSemester).WithMany(p => p.ReadmissionRecords)
                .HasForeignKey(d => d.RequestedSemesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__readmissi__reque__09A971A2");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.ReadmissionRecords)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK__readmissi__revie__0C85DE4D");

            entity.HasOne(d => d.Student).WithMany(p => p.ReadmissionRecords)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__readmissi__stude__08B54D69");
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(e => e.RegistrationId).HasName("PK__registra__22A298F604EBB00D");

            entity.ToTable("registrations");

            entity.HasIndex(e => e.AllocationId, "IX_reg_allocation");

            entity.HasIndex(e => e.Status, "IX_reg_status");

            entity.HasIndex(e => e.StudentId, "IX_reg_student");

            entity.HasIndex(e => new { e.StudentId, e.AllocationId }, "UQ_registrations").IsUnique();

            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.AllocationId).HasColumnName("allocation_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.AttemptNo)
                .HasDefaultValue(1)
                .HasColumnName("attempt_no");
            entity.Property(e => e.RegisteredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("registered_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending")
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Allocation).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.AllocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__registrat__alloc__00200768");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__registrat__appro__04E4BC85");

            entity.HasOne(d => d.Student).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__registrat__stude__7F2BE32F");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK__rooms__19675A8A77834770");

            entity.ToTable("rooms");

            entity.HasIndex(e => e.HostelId, "IX_rooms_hostel");

            entity.HasIndex(e => new { e.HostelId, e.RoomNumber }, "UQ_rooms_hostel_number").IsUnique();

            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.Capacity).HasColumnName("capacity");
            entity.Property(e => e.HostelId).HasColumnName("hostel_id");
            entity.Property(e => e.RoomNumber)
                .HasMaxLength(20)
                .HasColumnName("room_number");

            entity.HasOne(d => d.Hostel).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.HostelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__rooms__hostel_id__59063A47");
        });

        modelBuilder.Entity<Semester>(entity =>
        {
            entity.HasKey(e => e.SemesterId).HasName("PK__semester__CBC81B0177538AC0");

            entity.ToTable("semesters");

            entity.HasIndex(e => new { e.AcademicYearId, e.SemesterNo }, "UQ_semesters_year_no").IsUnique();

            entity.HasIndex(e => e.IsActive, "UX_semesters_one_active")
                .IsUnique()
                .HasFilter("([is_active]=(1))");

            entity.Property(e => e.SemesterId).HasColumnName("semester_id");
            entity.Property(e => e.AcademicYearId).HasColumnName("academic_year_id");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.SemesterName)
                .HasMaxLength(50)
                .HasColumnName("semester_name");
            entity.Property(e => e.SemesterNo).HasColumnName("semester_no");
            entity.Property(e => e.StartDate).HasColumnName("start_date");

            entity.HasOne(d => d.AcademicYear).WithMany(p => p.Semesters)
                .HasForeignKey(d => d.AcademicYearId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__semesters__acade__47DBAE45");
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasKey(e => e.StaffId).HasName("PK__staff__1963DD9C970354AF");

            entity.ToTable("staff");

            entity.HasIndex(e => e.DepartmentId, "IX_staff_department");

            entity.Property(e => e.StaffId)
                .ValueGeneratedNever()
                .HasColumnName("staff_id");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.EmploymentDetails)
                .HasMaxLength(500)
                .HasColumnName("employment_details");
            entity.Property(e => e.JobTitle)
                .HasMaxLength(100)
                .HasColumnName("job_title");

            entity.HasOne(d => d.Department).WithMany(p => p.Staff)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__staff__departmen__6477ECF3");

            entity.HasOne(d => d.StaffNavigation).WithOne(p => p.Staff)
                .HasForeignKey<Staff>(d => d.StaffId)
                .HasConstraintName("FK__staff__staff_id__6383C8BA");
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.StudentId).HasName("PK__students__2A33069A8AA9C459");

            entity.ToTable("students");

            entity.HasIndex(e => e.ProgrammeId, "IX_students_programme");

            entity.HasIndex(e => e.StudentNumber, "UQ__students__0E749A7966CD58C6").IsUnique();

            entity.Property(e => e.StudentId)
                .ValueGeneratedNever()
                .HasColumnName("student_id");
            entity.Property(e => e.EmergencyContact)
                .HasMaxLength(255)
                .HasColumnName("emergency_contact");
            entity.Property(e => e.EnrolmentStatus)
                .HasMaxLength(20)
                .HasDefaultValue("Enrolled")
                .HasColumnName("enrolment_status");
            entity.Property(e => e.ProgrammeId).HasColumnName("programme_id");
            entity.Property(e => e.StudentNumber)
                .HasMaxLength(20)
                .HasColumnName("student_number");

            entity.HasOne(d => d.Programme).WithMany(p => p.Students)
                .HasForeignKey(d => d.ProgrammeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__students__progra__5EBF139D");

            entity.HasOne(d => d.StudentNavigation).WithOne(p => p.Student)
                .HasForeignKey<Student>(d => d.StudentId)
                .HasConstraintName("FK__students__studen__5DCAEF64");
        });

        modelBuilder.Entity<Submission>(entity =>
        {
            entity.HasKey(e => e.SubmissionId).HasName("PK__submissi__9B535595387F96BE");

            entity.ToTable("submissions");

            entity.HasIndex(e => e.StudentId, "IX_sub_student");

            entity.HasIndex(e => new { e.AssignmentId, e.StudentId }, "UQ_submissions").IsUnique();

            entity.Property(e => e.SubmissionId).HasColumnName("submission_id");
            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.Feedback).HasColumnName("feedback");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .HasColumnName("file_url");
            entity.Property(e => e.IsLate).HasColumnName("is_late");
            entity.Property(e => e.MarksAwarded)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("marks_awarded");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("submitted_at");

            entity.HasOne(d => d.Assignment).WithMany(p => p.Submissions)
                .HasForeignKey(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__submissio__assig__1EA48E88");

            entity.HasOne(d => d.Student).WithMany(p => p.Submissions)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__submissio__stude__1F98B2C1");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__users__B9BE370FE10A1C08");

            entity.ToTable("users");

            entity.HasIndex(e => e.Role, "IX_users_role");

            entity.HasIndex(e => e.Email, "UQ__users__AB6E6164124FC221").IsUnique();

            entity.HasIndex(e => e.EntraId, "UQ__users__C4AD6C090DECB0C9").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.EntraId)
                .HasMaxLength(36)
                .HasColumnName("entra_id");
            entity.Property(e => e.Role)
                .HasMaxLength(30)
                .HasColumnName("role");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active")
                .HasColumnName("status");
        });

        modelBuilder.Entity<WelfareCase>(entity =>
        {
            entity.HasKey(e => e.CaseId).HasName("PK__welfare___A8FF8046884C4180");

            entity.ToTable("welfare_cases");

            entity.HasIndex(e => e.AssignedOfficerId, "IX_welfare_officer");

            entity.HasIndex(e => e.StudentId, "IX_welfare_student");

            entity.Property(e => e.CaseId).HasColumnName("case_id");
            entity.Property(e => e.AssignedOfficerId).HasColumnName("assigned_officer_id");
            entity.Property(e => e.CaseType)
                .HasMaxLength(50)
                .HasColumnName("case_type");
            entity.Property(e => e.DateLogged)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("date_logged");
            entity.Property(e => e.DateResolved).HasColumnName("date_resolved");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Open")
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.AssignedOfficer).WithMany(p => p.WelfareCases)
                .HasForeignKey(d => d.AssignedOfficerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__welfare_c__assig__3A4CA8FD");

            entity.HasOne(d => d.Student).WithMany(p => p.WelfareCases)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__welfare_c__stude__395884C4");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

[LCC_CMS_Schema.sql](https://github.com/user-attachments/files/31620452/LCC_CMS_Schema.sql)
/* =========================================================================
   LCC-CMS — Physical Database Schema v3 (T-SQL / Azure SQL Database)
   Workflow Step 7: Table Design — REVISION 3
   Rev 2 added: curriculum_versions, academic_years, readmission_records,
                documents, hostels, rooms, case_notes
   Rev 3 REMOVES curriculum_versions: curriculum content is set externally
   by the Lutheran Church body in line with national government policy, not
   published/versioned by the college — courses and students reference
   programmes directly again. academic_years, readmission_records, and the
   other Rev 2 entities are unaffected and remain.
   ========================================================================= */

-- -------------------------------------------------------------------------
-- 1. Academic structure
-- -------------------------------------------------------------------------
CREATE TABLE faculties (
    faculty_id      INT IDENTITY(1,1) PRIMARY KEY,
    faculty_name    NVARCHAR(150)   NOT NULL,
    CONSTRAINT UQ_faculties_name UNIQUE (faculty_name)
);

CREATE TABLE departments (
    department_id   INT IDENTITY(1,1) PRIMARY KEY,
    faculty_id      INT             NOT NULL REFERENCES faculties(faculty_id),
    department_name NVARCHAR(150)   NOT NULL,
    CONSTRAINT UQ_departments_name UNIQUE (faculty_id, department_name)
);
CREATE INDEX IX_departments_faculty ON departments(faculty_id);

CREATE TABLE programmes (
    programme_id    INT IDENTITY(1,1) PRIMARY KEY,
    department_id   INT             NOT NULL REFERENCES departments(department_id),
    programme_name  NVARCHAR(150)   NOT NULL,
    duration_years  DECIMAL(3,1)    NOT NULL CHECK (duration_years > 0),
    CONSTRAINT UQ_programmes_name UNIQUE (department_id, programme_name)
);
CREATE INDEX IX_programmes_department ON programmes(department_id);

CREATE TABLE academic_years (
    academic_year_id INT IDENTITY(1,1) PRIMARY KEY,
    year_name         NVARCHAR(9)    NOT NULL UNIQUE,     -- e.g. '2027'
    start_date        DATE           NOT NULL,
    end_date          DATE           NOT NULL,
    CONSTRAINT CK_academic_years_dates CHECK (end_date > start_date)
);

CREATE TABLE semesters (
    semester_id     INT IDENTITY(1,1) PRIMARY KEY,
    academic_year_id INT            NOT NULL REFERENCES academic_years(academic_year_id),
    semester_name   NVARCHAR(50)    NOT NULL,
    semester_no     TINYINT         NOT NULL CHECK (semester_no IN (1,2)),
    start_date      DATE            NOT NULL,
    end_date        DATE            NOT NULL,
    is_active       BIT             NOT NULL DEFAULT 0,
    CONSTRAINT CK_semesters_dates CHECK (end_date > start_date),
    CONSTRAINT UQ_semesters_year_no UNIQUE (academic_year_id, semester_no)
);
-- Business rule (SRS §2.5 / M3): exactly one active semester system-wide
CREATE UNIQUE INDEX UX_semesters_one_active ON semesters(is_active) WHERE is_active = 1;

-- -------------------------------------------------------------------------
-- 2. Identity (EER supertype/subtype: users -> students | staff)
-- -------------------------------------------------------------------------
CREATE TABLE users (
    user_id         INT IDENTITY(1,1) PRIMARY KEY,
    entra_id        NVARCHAR(36)    NOT NULL UNIQUE,
    email           NVARCHAR(255)   NOT NULL UNIQUE,
    role            NVARCHAR(30)    NOT NULL
        CHECK (role IN ('Student','Lecturer','HoD','Registrar/Admin','Management/Principal')),
    status          NVARCHAR(20)    NOT NULL DEFAULT 'Active'
        CHECK (status IN ('Active','Inactive')),
    created_at      DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_users_role ON users(role);

-- -------------------------------------------------------------------------
-- 3. Accommodation (hostels/rooms created before students reference them)
-- -------------------------------------------------------------------------
CREATE TABLE hostels (
    hostel_id       INT IDENTITY(1,1) PRIMARY KEY,
    hostel_name     NVARCHAR(100)   NOT NULL UNIQUE
);

CREATE TABLE rooms (
    room_id         INT IDENTITY(1,1) PRIMARY KEY,
    hostel_id       INT             NOT NULL REFERENCES hostels(hostel_id),
    room_number     NVARCHAR(20)    NOT NULL,
    capacity        INT             NOT NULL CHECK (capacity > 0),
    CONSTRAINT UQ_rooms_hostel_number UNIQUE (hostel_id, room_number)
);
CREATE INDEX IX_rooms_hostel ON rooms(hostel_id);

-- -------------------------------------------------------------------------
-- 4. Identity subtypes (students needs programmes; staff needs departments)
-- -------------------------------------------------------------------------
-- NOTE: curriculum_versions (Rev 2) has been removed. Curriculum content is set
-- externally by the Lutheran Church body in line with national government
-- requirements, not published by the college's Registrar/Admin — modeling it
-- as an internally-managed, versioned entity assumed an ownership process that
-- doesn't exist at the college level. students/courses now reference
-- programmes directly again, as in Rev 1.
CREATE TABLE students (
    student_id          INT             PRIMARY KEY REFERENCES users(user_id) ON DELETE CASCADE,
    student_number      NVARCHAR(20)    NOT NULL UNIQUE,
    programme_id         INT             NOT NULL REFERENCES programmes(programme_id),
    enrolment_status    NVARCHAR(20)    NOT NULL DEFAULT 'Enrolled'
        CHECK (enrolment_status IN ('Applied','Enrolled','Graduated','Withdrawn')),
    emergency_contact   NVARCHAR(255)   NULL
);
CREATE INDEX IX_students_programme ON students(programme_id);

CREATE TABLE staff (
    staff_id            INT             PRIMARY KEY REFERENCES users(user_id) ON DELETE CASCADE,
    department_id       INT             NOT NULL REFERENCES departments(department_id),
    job_title           NVARCHAR(100)   NOT NULL,
    employment_details  NVARCHAR(500)   NULL
);
CREATE INDEX IX_staff_department ON staff(department_id);

-- -------------------------------------------------------------------------
-- 6. Courses (units) & allocations
-- -------------------------------------------------------------------------
CREATE TABLE courses (
    course_id               INT IDENTITY(1,1) PRIMARY KEY,
    programme_id             INT             NOT NULL REFERENCES programmes(programme_id),
    course_code              NVARCHAR(20)    NOT NULL,
    course_name              NVARCHAR(150)   NOT NULL,
    credit_value              DECIMAL(4,1)    NOT NULL CHECK (credit_value > 0),
    year_level                TINYINT         NOT NULL CHECK (year_level BETWEEN 1 AND 3),
    semester_no                TINYINT         NOT NULL CHECK (semester_no IN (1,2)),
    is_core                   BIT             NOT NULL DEFAULT 1,
    prerequisite_course_id   INT             NULL REFERENCES courses(course_id),
    -- course_code is unique within a programme (a 3-year/6-semester structure
    -- with ~4 units per semester, per programme)
    CONSTRAINT UQ_courses_programme_code UNIQUE (programme_id, course_code)
);
CREATE INDEX IX_courses_programme ON courses(programme_id);
CREATE INDEX IX_courses_prerequisite ON courses(prerequisite_course_id);
CREATE INDEX IX_courses_year_semester ON courses(programme_id, year_level, semester_no);  -- "units per semester" queries

CREATE TABLE course_allocations (
    allocation_id   INT IDENTITY(1,1) PRIMARY KEY,
    course_id       INT NOT NULL REFERENCES courses(course_id),
    staff_id        INT NOT NULL REFERENCES staff(staff_id),
    semester_id     INT NOT NULL REFERENCES semesters(semester_id),
    CONSTRAINT UQ_course_allocations UNIQUE (course_id, staff_id, semester_id)
);
CREATE INDEX IX_alloc_course   ON course_allocations(course_id);
CREATE INDEX IX_alloc_staff    ON course_allocations(staff_id);
CREATE INDEX IX_alloc_semester ON course_allocations(semester_id);

-- -------------------------------------------------------------------------
-- 7. Admissions & registration
-- -------------------------------------------------------------------------
CREATE TABLE admissions (
    admission_id     INT IDENTITY(1,1) PRIMARY KEY,
    programme_id     INT             NOT NULL REFERENCES programmes(programme_id),
    applicant_name   NVARCHAR(150)   NOT NULL,
    applicant_email  NVARCHAR(255)   NOT NULL,
    applicant_phone  NVARCHAR(30)    NULL,
    date_of_birth    DATE            NULL,
    gender           NVARCHAR(20)    NULL,
    status           NVARCHAR(20)    NOT NULL DEFAULT 'Applied'
        CHECK (status IN ('Applied','Under Review','Approved','Rejected')),
    reviewed_by      INT             NULL REFERENCES staff(staff_id),
    decision_date    DATE            NULL,
    student_id       INT             NULL UNIQUE REFERENCES students(student_id), -- set on approval
    created_at       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_admissions_programme ON admissions(programme_id);
CREATE INDEX IX_admissions_status    ON admissions(status);

CREATE TABLE registrations (
    registration_id  INT IDENTITY(1,1) PRIMARY KEY,
    student_id       INT NOT NULL REFERENCES students(student_id),
    allocation_id    INT NOT NULL REFERENCES course_allocations(allocation_id),
    attempt_no       INT NOT NULL DEFAULT 1 CHECK (attempt_no > 0),
    status           NVARCHAR(20) NOT NULL DEFAULT 'Pending'
        CHECK (status IN ('Pending','Approved','Rejected','Dropped')),
    approved_by      INT NULL REFERENCES staff(staff_id),
    registered_at    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_registrations UNIQUE (student_id, allocation_id)
    -- attempt_no is set by the application: count of the student's prior
    -- Approved/Dropped/Rejected registrations against course_allocations
    -- sharing the same course_id, +1.
);
CREATE INDEX IX_reg_student    ON registrations(student_id);
CREATE INDEX IX_reg_allocation ON registrations(allocation_id);
CREATE INDEX IX_reg_status     ON registrations(status);

CREATE TABLE readmission_records (
    readmission_id          INT IDENTITY(1,1) PRIMARY KEY,
    student_id               INT NOT NULL REFERENCES students(student_id),
    requested_semester_id    INT NOT NULL REFERENCES semesters(semester_id),
    reason                   NVARCHAR(500) NOT NULL,
    status                   NVARCHAR(20) NOT NULL DEFAULT 'Pending'
        CHECK (status IN ('Pending','Approved','Rejected')),
    reviewed_by              INT NULL REFERENCES staff(staff_id),
    decision_date            DATE NULL,
    created_at               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_readmission_student ON readmission_records(student_id);
CREATE INDEX IX_readmission_status  ON readmission_records(status);

-- -------------------------------------------------------------------------
-- 8. Attendance
-- -------------------------------------------------------------------------
CREATE TABLE attendance_sessions (
    session_id      INT IDENTITY(1,1) PRIMARY KEY,
    allocation_id   INT NOT NULL REFERENCES course_allocations(allocation_id),
    session_date    DATE NOT NULL,
    CONSTRAINT UQ_attendance_sessions UNIQUE (allocation_id, session_date)
);
CREATE INDEX IX_sessions_allocation ON attendance_sessions(allocation_id);

CREATE TABLE attendances (
    attendance_id   INT IDENTITY(1,1) PRIMARY KEY,
    session_id      INT NOT NULL REFERENCES attendance_sessions(session_id),
    student_id      INT NOT NULL REFERENCES students(student_id),
    status          NVARCHAR(15) NOT NULL CHECK (status IN ('Present','Absent','Late','Excused')),
    CONSTRAINT UQ_attendances UNIQUE (session_id, student_id)
);
CREATE INDEX IX_att_student ON attendances(student_id);
CREATE INDEX IX_att_status  ON attendances(status);

-- -------------------------------------------------------------------------
-- 9. Learning, assignments & assessment
-- -------------------------------------------------------------------------
CREATE TABLE assignments (
    assignment_id   INT IDENTITY(1,1) PRIMARY KEY,
    allocation_id   INT NOT NULL REFERENCES course_allocations(allocation_id),
    title           NVARCHAR(150) NOT NULL,
    instructions    NVARCHAR(MAX) NULL,
    due_date        DATETIME2 NOT NULL,
    max_marks       DECIMAL(5,2) NOT NULL CHECK (max_marks > 0)
);
CREATE INDEX IX_assignments_allocation ON assignments(allocation_id);

CREATE TABLE submissions (
    submission_id   INT IDENTITY(1,1) PRIMARY KEY,
    assignment_id   INT NOT NULL REFERENCES assignments(assignment_id),
    student_id      INT NOT NULL REFERENCES students(student_id),
    file_url        NVARCHAR(500) NOT NULL,
    submitted_at    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    is_late         BIT NOT NULL DEFAULT 0,
    marks_awarded   DECIMAL(5,2) NULL,
    feedback        NVARCHAR(MAX) NULL,
    CONSTRAINT UQ_submissions UNIQUE (assignment_id, student_id)
);
CREATE INDEX IX_sub_student ON submissions(student_id);

CREATE TABLE assessments (
    assessment_id   INT IDENTITY(1,1) PRIMARY KEY,
    allocation_id   INT NOT NULL REFERENCES course_allocations(allocation_id),
    title           NVARCHAR(150) NOT NULL,
    weight_percent  DECIMAL(5,2) NOT NULL CHECK (weight_percent > 0 AND weight_percent <= 100),
    max_marks       DECIMAL(5,2) NOT NULL CHECK (max_marks > 0)
);
CREATE INDEX IX_assessments_allocation ON assessments(allocation_id);

CREATE TABLE grades (
    grade_id                INT IDENTITY(1,1) PRIMARY KEY,
    assessment_id           INT NOT NULL REFERENCES assessments(assessment_id),
    student_id              INT NOT NULL REFERENCES students(student_id),
    marks_obtained          DECIMAL(5,2) NOT NULL CHECK (marks_obtained >= 0),
    grade_letter            NVARCHAR(3) NULL CHECK (grade_letter IN ('HD','D','C','P','F+','F') OR grade_letter IS NULL),
    published               BIT NOT NULL DEFAULT 0,
    overridden_by           INT NULL REFERENCES staff(staff_id),
    override_justification  NVARCHAR(500) NULL,
    CONSTRAINT UQ_grades UNIQUE (assessment_id, student_id)
);
CREATE INDEX IX_grades_student    ON grades(student_id);
CREATE INDEX IX_grades_published  ON grades(published);

-- -------------------------------------------------------------------------
-- 10. Accommodation & welfare
-- -------------------------------------------------------------------------
CREATE TABLE accommodation_records (
    accommodation_id  INT IDENTITY(1,1) PRIMARY KEY,
    student_id        INT NOT NULL REFERENCES students(student_id),
    room_id           INT NOT NULL REFERENCES rooms(room_id),
    status            NVARCHAR(15) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Vacated')),
    allocated_by      INT NOT NULL REFERENCES staff(staff_id),
    date_allocated    DATE NOT NULL DEFAULT CAST(SYSUTCDATETIME() AS DATE),
    date_vacated      DATE NULL
);
CREATE INDEX IX_accommodation_room ON accommodation_records(room_id);
-- Business rule (SRS §6.1): at most one ACTIVE accommodation record per student
CREATE UNIQUE INDEX UX_accommodation_one_active ON accommodation_records(student_id) WHERE status = 'Active';
-- NOTE: capacity vs. current-occupant-count for a room is NOT a schema-level
-- constraint (it requires counting sibling rows against rooms.capacity, a
-- cross-table comparison) — enforce in the application/API layer when
-- allocating a room, same pattern as marks_obtained <= max_marks.

CREATE TABLE welfare_cases (
    case_id              INT IDENTITY(1,1) PRIMARY KEY,
    student_id           INT NOT NULL REFERENCES students(student_id),
    assigned_officer_id  INT NOT NULL REFERENCES staff(staff_id),
    case_type            NVARCHAR(50) NOT NULL,
    status               NVARCHAR(20) NOT NULL DEFAULT 'Open' CHECK (status IN ('Open','In Progress','Resolved')),
    date_logged          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    date_resolved        DATETIME2 NULL
);
CREATE INDEX IX_welfare_student ON welfare_cases(student_id);
CREATE INDEX IX_welfare_officer ON welfare_cases(assigned_officer_id);

CREATE TABLE case_notes (
    case_note_id    INT IDENTITY(1,1) PRIMARY KEY,
    case_id         INT NOT NULL REFERENCES welfare_cases(case_id),
    staff_id        INT NOT NULL REFERENCES staff(staff_id),
    note            NVARCHAR(1000) NOT NULL,
    created_at      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_case_notes_case ON case_notes(case_id);

-- -------------------------------------------------------------------------
-- 11. Communication
-- -------------------------------------------------------------------------
CREATE TABLE notices (
    notice_id    INT IDENTITY(1,1) PRIMARY KEY,
    author_id    INT NOT NULL REFERENCES staff(staff_id),
    title        NVARCHAR(150) NOT NULL,
    content      NVARCHAR(MAX) NOT NULL,
    target_role  NVARCHAR(30) NULL
        CHECK (target_role IS NULL OR target_role IN ('Student','Lecturer','HoD','Registrar/Admin','Management/Principal')),
    posted_at    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_notices_posted_at ON notices(posted_at);

CREATE TABLE messages (
    message_id    INT IDENTITY(1,1) PRIMARY KEY,
    sender_id     INT NOT NULL REFERENCES users(user_id),
    recipient_id  INT NOT NULL REFERENCES users(user_id),
    content       NVARCHAR(2000) NOT NULL,
    sent_at       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    is_deleted    BIT NOT NULL DEFAULT 0,
    CONSTRAINT CK_messages_not_blank CHECK (LEN(LTRIM(RTRIM(content))) > 0),
    CONSTRAINT CK_messages_no_self  CHECK (sender_id <> recipient_id)
);
CREATE INDEX IX_messages_sender    ON messages(sender_id);
CREATE INDEX IX_messages_recipient ON messages(recipient_id);

-- -------------------------------------------------------------------------
-- 12. Documents
-- -------------------------------------------------------------------------
CREATE TABLE documents (
    document_id     INT IDENTITY(1,1) PRIMARY KEY,
    student_id      INT NOT NULL REFERENCES students(student_id),
    document_type   NVARCHAR(30) NOT NULL
        CHECK (document_type IN ('Profile Photo','Birth Certificate','National ID','Academic Transcript','Other')),
    file_url        NVARCHAR(500) NOT NULL,
    uploaded_at     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_documents_student ON documents(student_id);

-- -------------------------------------------------------------------------
-- 13. Audit
-- -------------------------------------------------------------------------
CREATE TABLE audit_logs (
    audit_id     BIGINT IDENTITY(1,1) PRIMARY KEY,
    user_id      INT NOT NULL REFERENCES users(user_id),
    action       NVARCHAR(10) NOT NULL CHECK (action IN ('Create','Update','Delete')),
    table_name   NVARCHAR(100) NOT NULL,
    record_id    NVARCHAR(50) NOT NULL,
    old_value    NVARCHAR(MAX) NULL,
    new_value    NVARCHAR(MAX) NULL,
    [timestamp]  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_audit_user           ON audit_logs(user_id);
CREATE INDEX IX_audit_table_record   ON audit_logs(table_name, record_id);
CREATE INDEX IX_audit_timestamp      ON audit_logs([timestamp]);

/* =========================================================================
   End of schema. 29 tables, in FK-safe creation order.
   ========================================================================= */

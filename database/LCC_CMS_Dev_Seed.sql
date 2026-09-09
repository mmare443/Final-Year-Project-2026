/* =========================================================================
   LCC-CMS — development seed (empty LCCCMSDB)
   Date: 2026-09-09
   Does not set users.password_hash. After this script, restart the API so
   LabPasswordSeeder hashes JwtSettings:LabPassword (LccCms!2026).

   Prerequisite order:
     1. database/LCC_CMS_Schema.sql (Rev 3)
     2. database/LCC_CMS_Schema_Upgrade_Rev4_Fixed.sql
     3. database/LCC_CMS_Schema_Upgrade_Rev5_LocalAuth.sql
     4. this file

   Target: LCCCMSDB
   ========================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.faculties', N'U') IS NULL
    THROW 50031, N'Seed abort: Rev 3 schema is missing. Run LCC_CMS_Schema.sql first.', 1;
IF OBJECT_ID(N'dbo.users', N'U') IS NULL
    THROW 50032, N'Seed abort: dbo.users is missing.', 1;

BEGIN TRANSACTION;

    /* ----- 1. Academic structure ----- */

    IF NOT EXISTS (SELECT 1 FROM dbo.faculties WHERE faculty_name = N'Faculty of Applied Studies')
        INSERT INTO dbo.faculties (faculty_name)
        VALUES (N'Faculty of Applied Studies');

    DECLARE @faculty_id INT =
        (SELECT faculty_id FROM dbo.faculties WHERE faculty_name = N'Faculty of Applied Studies');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.departments
        WHERE faculty_id = @faculty_id AND department_name = N'Department of Business Administration'
    )
        INSERT INTO dbo.departments (faculty_id, department_name)
        VALUES (@faculty_id, N'Department of Business Administration');

    DECLARE @department_id INT =
        (SELECT department_id FROM dbo.departments
         WHERE faculty_id = @faculty_id AND department_name = N'Department of Business Administration');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.programmes
        WHERE department_id = @department_id
          AND programme_name = N'Diploma in Business Administration and Management'
    )
        INSERT INTO dbo.programmes (department_id, programme_name, duration_years)
        VALUES (@department_id, N'Diploma in Business Administration and Management', 3.0);

    DECLARE @programme_id INT =
        (SELECT programme_id FROM dbo.programmes
         WHERE department_id = @department_id
           AND programme_name = N'Diploma in Business Administration and Management');

    IF NOT EXISTS (SELECT 1 FROM dbo.academic_years WHERE year_name = N'2026')
        INSERT INTO dbo.academic_years (year_name, start_date, end_date)
        VALUES (N'2026', '2026-02-01', '2026-11-30');

    DECLARE @academic_year_id INT =
        (SELECT academic_year_id FROM dbo.academic_years WHERE year_name = N'2026');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.semesters
        WHERE academic_year_id = @academic_year_id AND semester_no = 1
    )
        INSERT INTO dbo.semesters (
            academic_year_id, semester_name, semester_no, start_date, end_date, is_active
        )
        VALUES (
            @academic_year_id, N'Semester 1, 2026', 1, '2026-02-01', '2026-06-30', 1
        );
    ELSE
        UPDATE dbo.semesters
        SET is_active = 1
        WHERE academic_year_id = @academic_year_id AND semester_no = 1
          AND is_active = 0
          AND NOT EXISTS (SELECT 1 FROM dbo.semesters WHERE is_active = 1);

    DECLARE @semester_id INT =
        (SELECT semester_id FROM dbo.semesters
         WHERE academic_year_id = @academic_year_id AND semester_no = 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.semesters WHERE semester_id = @semester_id AND is_active = 1)
        THROW 50033, N'Seed abort: could not mark Semester 1 2026 as the single active semester (another semester may already be active).', 1;

    /* ----- 2. Users (password_hash omitted / NULL for LabPasswordSeeder) ----- */

    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'student@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-0000-0000-000000000001', N'student@lccb.ac.pg', N'Student', N'Active');

    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'lecturer@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-0000-0000-000000000002', N'lecturer@lccb.ac.pg', N'Lecturer', N'Active');

    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'hod@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-0000-0000-000000000003', N'hod@lccb.ac.pg', N'HoD', N'Active');

    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'registrar@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-0000-0000-000000000004', N'registrar@lccb.ac.pg', N'Registrar/Admin', N'Active');

    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'principal@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-0000-0000-000000000005', N'principal@lccb.ac.pg', N'Management/Principal', N'Active');

    DECLARE @student_user_id INT =
        (SELECT user_id FROM dbo.users WHERE email = N'student@lccb.ac.pg');
    DECLARE @lecturer_user_id INT =
        (SELECT user_id FROM dbo.users WHERE email = N'lecturer@lccb.ac.pg');
    DECLARE @hod_user_id INT =
        (SELECT user_id FROM dbo.users WHERE email = N'hod@lccb.ac.pg');
    DECLARE @registrar_user_id INT =
        (SELECT user_id FROM dbo.users WHERE email = N'registrar@lccb.ac.pg');
    DECLARE @principal_user_id INT =
        (SELECT user_id FROM dbo.users WHERE email = N'principal@lccb.ac.pg');

    /* Student subtype (PK = users.user_id) */
    IF NOT EXISTS (SELECT 1 FROM dbo.students WHERE student_id = @student_user_id)
        INSERT INTO dbo.students (student_id, student_number, programme_id, enrolment_status)
        VALUES (@student_user_id, N'LCC26001', @programme_id, N'Enrolled');

    /* Staff subtypes (PK = users.user_id) */
    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @lecturer_user_id)
        INSERT INTO dbo.staff (staff_id, department_id, job_title)
        VALUES (@lecturer_user_id, @department_id, N'Lecturer');

    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @hod_user_id)
        INSERT INTO dbo.staff (staff_id, department_id, job_title)
        VALUES (@hod_user_id, @department_id, N'Head of Department');

    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @registrar_user_id)
        INSERT INTO dbo.staff (staff_id, department_id, job_title)
        VALUES (@registrar_user_id, @department_id, N'Registrar');

    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @principal_user_id)
        INSERT INTO dbo.staff (staff_id, department_id, job_title)
        VALUES (@principal_user_id, @department_id, N'Principal');

    /* ----- 3. Courses, allocation, registration ----- */

    IF NOT EXISTS (
        SELECT 1 FROM dbo.courses
        WHERE programme_id = @programme_id AND course_code = N'BAM101'
    )
        INSERT INTO dbo.courses (
            programme_id, course_code, course_name, credit_value,
            year_level, semester_no, is_core, prerequisite_course_id
        )
        VALUES (
            @programme_id, N'BAM101', N'Introduction to Management', 15.0,
            1, 1, 1, NULL
        );

    IF NOT EXISTS (
        SELECT 1 FROM dbo.courses
        WHERE programme_id = @programme_id AND course_code = N'BAM102'
    )
        INSERT INTO dbo.courses (
            programme_id, course_code, course_name, credit_value,
            year_level, semester_no, is_core, prerequisite_course_id
        )
        VALUES (
            @programme_id, N'BAM102', N'Business Communication', 15.0,
            1, 1, 1, NULL
        );

    IF NOT EXISTS (
        SELECT 1 FROM dbo.courses
        WHERE programme_id = @programme_id AND course_code = N'BAM103'
    )
        INSERT INTO dbo.courses (
            programme_id, course_code, course_name, credit_value,
            year_level, semester_no, is_core, prerequisite_course_id
        )
        VALUES (
            @programme_id, N'BAM103', N'Principles of Accounting', 15.0,
            1, 1, 1, NULL
        );

    DECLARE @course_bam101 INT =
        (SELECT course_id FROM dbo.courses
         WHERE programme_id = @programme_id AND course_code = N'BAM101');
    DECLARE @course_bam102 INT =
        (SELECT course_id FROM dbo.courses
         WHERE programme_id = @programme_id AND course_code = N'BAM102');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.course_allocations
        WHERE course_id = @course_bam101
          AND staff_id = @lecturer_user_id
          AND semester_id = @semester_id
    )
        INSERT INTO dbo.course_allocations (course_id, staff_id, semester_id)
        VALUES (@course_bam101, @lecturer_user_id, @semester_id);

    IF NOT EXISTS (
        SELECT 1 FROM dbo.course_allocations
        WHERE course_id = @course_bam102
          AND staff_id = @lecturer_user_id
          AND semester_id = @semester_id
    )
        INSERT INTO dbo.course_allocations (course_id, staff_id, semester_id)
        VALUES (@course_bam102, @lecturer_user_id, @semester_id);

    DECLARE @allocation_bam101 INT =
        (SELECT allocation_id FROM dbo.course_allocations
         WHERE course_id = @course_bam101
           AND staff_id = @lecturer_user_id
           AND semester_id = @semester_id);

    IF NOT EXISTS (
        SELECT 1 FROM dbo.registrations
        WHERE student_id = @student_user_id AND allocation_id = @allocation_bam101
    )
        INSERT INTO dbo.registrations (
            student_id, allocation_id, attempt_no, status, approved_by
        )
        VALUES (
            @student_user_id, @allocation_bam101, 1, N'Approved', @registrar_user_id
        );

COMMIT TRANSACTION;

PRINT N'Development seed committed.';
PRINT N'Restart the API so LabPasswordSeeder can hash LccCms!2026 onto NULL password_hash rows.';

SELECT email, role, status,
       CASE WHEN COL_LENGTH(N'dbo.users', N'password_hash') IS NULL THEN N'(column missing — run Rev5)'
            WHEN password_hash IS NULL THEN N'NULL (seeder will hash)'
            ELSE N'already hashed' END AS password_hash_state
FROM dbo.users
ORDER BY user_id;

GO

/* Login after API restart (all passwords LccCms!2026):
     student@lccb.ac.pg      Student
     lecturer@lccb.ac.pg     Lecturer
     hod@lccb.ac.pg          HoD
     registrar@lccb.ac.pg    RegistrarAdmin (SQL: Registrar/Admin)
     principal@lccb.ac.pg    ManagementPrincipal (SQL: Management/Principal)
*/

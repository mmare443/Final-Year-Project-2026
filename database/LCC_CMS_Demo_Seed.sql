/* =========================================================================
   LCC-CMS — demonstration seed (LCCCMSDB)
   Date: 2026-09-11

   Prerequisite:
     Schema Rev 3 + Rev 4/5
     Academic structure already present (Dev_Seed + LccbStructure, or Dev_Seed
     after the LCCB update). This script looks up faculties/programmes by name
     and does not drop or recreate them.

   Safe to run once; re-run is a no-op (IF NOT EXISTS on natural keys).
   Does not set password_hash — restart the API so LabPasswordSeeder hashes
   LccCms!2026 onto NULL hashes.

   Notes:
     - students.enrolment_status has no 'Pending'; demo "pending" uses 'Applied'.
     - grades.grade_letter CHECK is A/B/C/D/F only (no B+/C+). Variation is
       A/B/C (and one D) via marks that map to those letters.
   ========================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

USE LCCCMSDB;

IF OBJECT_ID(N'dbo.faculties', N'U') IS NULL
    THROW 50051, N'Demo seed abort: schema missing.', 1;

DECLARE @prog_bam_dip INT = (
    SELECT p.programme_id
    FROM dbo.programmes p
    JOIN dbo.departments d ON d.department_id = p.department_id
    WHERE p.programme_name = N'Diploma in Business Administration and Management');
DECLARE @prog_bam_cert INT = (
    SELECT p.programme_id FROM dbo.programmes p
    JOIN dbo.departments d ON d.department_id = p.department_id
    WHERE p.programme_name = N'Certificate in Business Administration and Management');
DECLARE @prog_min_dip INT = (
    SELECT p.programme_id FROM dbo.programmes p
    WHERE p.programme_name = N'Diploma in Applied Ministry');
DECLARE @prog_min_cert INT = (
    SELECT p.programme_id FROM dbo.programmes p
    WHERE p.programme_name = N'Certificate in Applied Ministry');
DECLARE @prog_agr_dip INT = (
    SELECT p.programme_id FROM dbo.programmes p
    WHERE p.programme_name = N'Diploma in Tropical Agriculture');
DECLARE @prog_agr_cert INT = (
    SELECT p.programme_id FROM dbo.programmes p
    WHERE p.programme_name = N'Certificate in Tropical Agriculture');

IF @prog_bam_dip IS NULL OR @prog_bam_cert IS NULL
   OR @prog_min_dip IS NULL OR @prog_min_cert IS NULL
   OR @prog_agr_dip IS NULL OR @prog_agr_cert IS NULL
    THROW 50052, N'Demo seed abort: LCCB programmes missing. Run LCC_CMS_Dev_Seed.sql / LCC_CMS_Seed_Update_LccbStructure.sql first.', 1;

DECLARE @dept_bus INT = (
    SELECT department_id FROM dbo.departments
    WHERE department_name = N'Department of Business Administration');
DECLARE @dept_min INT = (
    SELECT department_id FROM dbo.departments
    WHERE department_name = N'Department of Applied Ministry');
DECLARE @dept_agr INT = (
    SELECT department_id FROM dbo.departments
    WHERE department_name = N'Department of Tropical Agriculture');

IF NOT EXISTS (SELECT 1 FROM dbo.academic_years WHERE year_name = N'2026')
    INSERT INTO dbo.academic_years (year_name, start_date, end_date)
    VALUES (N'2026', '2026-02-01', '2026-11-30');

DECLARE @year_id INT = (SELECT academic_year_id FROM dbo.academic_years WHERE year_name = N'2026');

IF NOT EXISTS (
    SELECT 1 FROM dbo.semesters WHERE academic_year_id = @year_id AND semester_no = 1
)
    INSERT INTO dbo.semesters (academic_year_id, semester_name, semester_no, start_date, end_date, is_active)
    VALUES (@year_id, N'Semester 1, 2026', 1, '2026-02-01', '2026-06-30', 1);

DECLARE @sem_id INT = (
    SELECT semester_id FROM dbo.semesters
    WHERE academic_year_id = @year_id AND semester_no = 1);

BEGIN TRANSACTION;

    /* ----- Staff (reuse lab Principal / Registrar / Business HoD / Business lecturer) ----- */

    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'principal@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-0000-0000-000000000005', N'principal@lccb.ac.pg', N'Management/Principal', N'Active');
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'registrar@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-0000-0000-000000000004', N'registrar@lccb.ac.pg', N'Registrar/Admin', N'Active');
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'hod@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-0000-0000-000000000003', N'hod@lccb.ac.pg', N'HoD', N'Active');
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'hod.ministry@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-4000-8000-000000000211', N'hod.ministry@lccb.ac.pg', N'HoD', N'Active');
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'hod.agriculture@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-4000-8000-000000000212', N'hod.agriculture@lccb.ac.pg', N'HoD', N'Active');
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'lecturer@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-0000-0000-000000000002', N'lecturer@lccb.ac.pg', N'Lecturer', N'Active');
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'lecturer.kila@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-4000-8000-000000000221', N'lecturer.kila@lccb.ac.pg', N'Lecturer', N'Active');
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'lecturer.ministry@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-4000-8000-000000000222', N'lecturer.ministry@lccb.ac.pg', N'Lecturer', N'Active');
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE email = N'lecturer.agriculture@lccb.ac.pg')
        INSERT INTO dbo.users (entra_id, email, role, status)
        VALUES (N'00000000-0000-4000-8000-000000000223', N'lecturer.agriculture@lccb.ac.pg', N'Lecturer', N'Active');

    DECLARE @principal INT = (SELECT user_id FROM dbo.users WHERE email = N'principal@lccb.ac.pg');
    DECLARE @registrar INT = (SELECT user_id FROM dbo.users WHERE email = N'registrar@lccb.ac.pg');
    DECLARE @hod_bus INT = (SELECT user_id FROM dbo.users WHERE email = N'hod@lccb.ac.pg');
    DECLARE @hod_min INT = (SELECT user_id FROM dbo.users WHERE email = N'hod.ministry@lccb.ac.pg');
    DECLARE @hod_agr INT = (SELECT user_id FROM dbo.users WHERE email = N'hod.agriculture@lccb.ac.pg');
    DECLARE @lec_bus1 INT = (SELECT user_id FROM dbo.users WHERE email = N'lecturer@lccb.ac.pg');
    DECLARE @lec_bus2 INT = (SELECT user_id FROM dbo.users WHERE email = N'lecturer.kila@lccb.ac.pg');
    DECLARE @lec_min INT = (SELECT user_id FROM dbo.users WHERE email = N'lecturer.ministry@lccb.ac.pg');
    DECLARE @lec_agr INT = (SELECT user_id FROM dbo.users WHERE email = N'lecturer.agriculture@lccb.ac.pg');

    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @principal)
        INSERT INTO dbo.staff (staff_id, department_id, job_title, employment_details)
        VALUES (@principal, @dept_bus, N'Principal', N'College Principal — LCCB');
    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @registrar)
        INSERT INTO dbo.staff (staff_id, department_id, job_title, employment_details)
        VALUES (@registrar, @dept_bus, N'Registrar', N'Registrar / Academic administration');
    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @hod_bus)
        INSERT INTO dbo.staff (staff_id, department_id, job_title, employment_details)
        VALUES (@hod_bus, @dept_bus, N'Head of Department', N'HoD, Business Administration');
    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @hod_min)
        INSERT INTO dbo.staff (staff_id, department_id, job_title, employment_details)
        VALUES (@hod_min, @dept_min, N'Head of Department', N'HoD, Applied Ministry');
    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @hod_agr)
        INSERT INTO dbo.staff (staff_id, department_id, job_title, employment_details)
        VALUES (@hod_agr, @dept_agr, N'Head of Department', N'HoD, Tropical Agriculture');
    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @lec_bus1)
        INSERT INTO dbo.staff (staff_id, department_id, job_title, employment_details)
        VALUES (@lec_bus1, @dept_bus, N'Lecturer', N'Lecturer, Business Administration');
    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @lec_bus2)
        INSERT INTO dbo.staff (staff_id, department_id, job_title, employment_details)
        VALUES (@lec_bus2, @dept_bus, N'Lecturer', N'Lecturer, Business Administration (units 104–105 / certificate)');
    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @lec_min)
        INSERT INTO dbo.staff (staff_id, department_id, job_title, employment_details)
        VALUES (@lec_min, @dept_min, N'Lecturer', N'Lecturer, Applied Ministry');
    IF NOT EXISTS (SELECT 1 FROM dbo.staff WHERE staff_id = @lec_agr)
        INSERT INTO dbo.staff (staff_id, department_id, job_title, employment_details)
        VALUES (@lec_agr, @dept_agr, N'Lecturer', N'Lecturer, Tropical Agriculture');

    /* ----- Courses (5 per diploma / certificate) ----- */

    DECLARE @courses TABLE (
        programme_id INT NOT NULL,
        course_code NVARCHAR(20) NOT NULL,
        course_name NVARCHAR(150) NOT NULL
    );
    INSERT INTO @courses (programme_id, course_code, course_name) VALUES
        (@prog_bam_dip, N'BAM101', N'Introduction to Management'),
        (@prog_bam_dip, N'BAM102', N'Business Communication'),
        (@prog_bam_dip, N'BAM103', N'Principles of Accounting'),
        (@prog_bam_dip, N'BAM104', N'Marketing Fundamentals'),
        (@prog_bam_dip, N'BAM105', N'Business Mathematics'),
        (@prog_bam_cert, N'CBT101', N'Office Procedures'),
        (@prog_bam_cert, N'CBT102', N'Basic Bookkeeping'),
        (@prog_bam_cert, N'CBT103', N'Customer Service'),
        (@prog_bam_cert, N'CBT104', N'Computer Applications'),
        (@prog_bam_cert, N'CBT105', N'Workplace Skills'),
        (@prog_min_dip, N'MIN101', N'Biblical Foundations'),
        (@prog_min_dip, N'MIN102', N'Pastoral Care'),
        (@prog_min_dip, N'MIN103', N'Homiletics'),
        (@prog_min_dip, N'MIN104', N'Church Administration'),
        (@prog_min_dip, N'MIN105', N'Christian Ethics'),
        (@prog_min_cert, N'MNC101', N'Introduction to Ministry'),
        (@prog_min_cert, N'MNC102', N'Worship Leading'),
        (@prog_min_cert, N'MNC103', N'Youth Ministry'),
        (@prog_min_cert, N'MNC104', N'Community Outreach'),
        (@prog_min_cert, N'MNC105', N'Scripture Survey'),
        (@prog_agr_dip, N'AGR101', N'Tropical Crop Production'),
        (@prog_agr_dip, N'AGR102', N'Soil and Water Management'),
        (@prog_agr_dip, N'AGR103', N'Farm Business'),
        (@prog_agr_dip, N'AGR104', N'Livestock Husbandry'),
        (@prog_agr_dip, N'AGR105', N'Agricultural Extension'),
        (@prog_agr_cert, N'AGC101', N'Garden Production'),
        (@prog_agr_cert, N'AGC102', N'Compost and Soil Health'),
        (@prog_agr_cert, N'AGC103', N'Planting Calendars'),
        (@prog_agr_cert, N'AGC104', N'Pest Awareness'),
        (@prog_agr_cert, N'AGC105', N'Harvest Handling');

    INSERT INTO dbo.courses (programme_id, course_code, course_name, credit_value, year_level, semester_no, is_core)
    SELECT c.programme_id, c.course_code, c.course_name, 15.0, 1, 1, 1
    FROM @courses c
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.courses x
        WHERE x.programme_id = c.programme_id AND x.course_code = c.course_code
    );

    DECLARE @alloc_staff TABLE (course_code NVARCHAR(20) NOT NULL, staff_id INT NOT NULL);
    INSERT INTO @alloc_staff (course_code, staff_id) VALUES
        (N'BAM101', @lec_bus1), (N'BAM102', @lec_bus1), (N'BAM103', @lec_bus1),
        (N'BAM104', @lec_bus2), (N'BAM105', @lec_bus2),
        (N'CBT101', @lec_bus2), (N'CBT102', @lec_bus2), (N'CBT103', @lec_bus2),
        (N'CBT104', @lec_bus1), (N'CBT105', @lec_bus1),
        (N'MIN101', @lec_min), (N'MIN102', @lec_min), (N'MIN103', @lec_min),
        (N'MIN104', @lec_min), (N'MIN105', @lec_min),
        (N'MNC101', @lec_min), (N'MNC102', @lec_min), (N'MNC103', @lec_min),
        (N'MNC104', @lec_min), (N'MNC105', @lec_min),
        (N'AGR101', @lec_agr), (N'AGR102', @lec_agr), (N'AGR103', @lec_agr),
        (N'AGR104', @lec_agr), (N'AGR105', @lec_agr),
        (N'AGC101', @lec_agr), (N'AGC102', @lec_agr), (N'AGC103', @lec_agr),
        (N'AGC104', @lec_agr), (N'AGC105', @lec_agr);

    INSERT INTO dbo.course_allocations (course_id, staff_id, semester_id)
    SELECT c.course_id, a.staff_id, @sem_id
    FROM dbo.courses c
    JOIN @alloc_staff a ON a.course_code = c.course_code
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.course_allocations x
        WHERE x.course_id = c.course_id AND x.staff_id = a.staff_id AND x.semester_id = @sem_id
    );

    /* ----- 18 students ----- */

    DECLARE @stu TABLE (
        email NVARCHAR(255) NOT NULL,
        entra NVARCHAR(36) NOT NULL,
        student_number NVARCHAR(20) NOT NULL,
        programme_id INT NOT NULL,
        enrolment_status NVARCHAR(20) NOT NULL,
        applicant_name NVARCHAR(150) NOT NULL
    );
    INSERT INTO @stu VALUES
        (N'student@lccb.ac.pg', N'00000000-0000-0000-0000-000000000001', N'LCC26001', @prog_bam_dip, N'Enrolled', N'Kila Student'),
        (N'm.mendi@lccb.ac.pg', N'00000000-0000-4000-8000-000000000301', N'LCC26002', @prog_bam_dip, N'Enrolled', N'Mary Mendi'),
        (N'j.waigani@lccb.ac.pg', N'00000000-0000-4000-8000-000000000302', N'LCC26003', @prog_bam_dip, N'Enrolled', N'John Waigani'),
        (N'r.lae@lccb.ac.pg', N'00000000-0000-4000-8000-000000000303', N'LCC26004', @prog_bam_dip, N'Enrolled', N'Ruth Lae'),
        (N'p.goroka@lccb.ac.pg', N'00000000-0000-4000-8000-000000000304', N'LCC26005', @prog_bam_dip, N'Enrolled', N'Peter Goroka'),
        (N'a.madang@lccb.ac.pg', N'00000000-0000-4000-8000-000000000305', N'LCC26006', @prog_bam_cert, N'Enrolled', N'Anna Madang'),
        (N'd.rabaul@lccb.ac.pg', N'00000000-0000-4000-8000-000000000306', N'LCC26007', @prog_bam_dip, N'Applied', N'David Rabaul'),
        (N's.wewak@lccb.ac.pg', N'00000000-0000-4000-8000-000000000307', N'LCC26008', @prog_bam_dip, N'Graduated', N'Sarah Wewak'),
        (N't.hagen@lccb.ac.pg', N'00000000-0000-4000-8000-000000000308', N'LCC26009', @prog_min_dip, N'Enrolled', N'Thomas Hagen'),
        (N'l.kerema@lccb.ac.pg', N'00000000-0000-4000-8000-000000000309', N'LCC26010', @prog_min_dip, N'Enrolled', N'Lina Kerema'),
        (N'b.alotau@lccb.ac.pg', N'00000000-0000-4000-8000-000000000310', N'LCC26011', @prog_min_dip, N'Enrolled', N'Ben Alotau'),
        (N'e.kimbe@lccb.ac.pg', N'00000000-0000-4000-8000-000000000311', N'LCC26012', @prog_min_cert, N'Applied', N'Esther Kimbe'),
        (N'g.vanimo@lccb.ac.pg', N'00000000-0000-4000-8000-000000000312', N'LCC26013', @prog_min_dip, N'Graduated', N'Grace Vanimo'),
        (N'h.popondetta@lccb.ac.pg', N'00000000-0000-4000-8000-000000000313', N'LCC26014', @prog_agr_dip, N'Enrolled', N'Helen Popondetta'),
        (N'n.kavieng@lccb.ac.pg', N'00000000-0000-4000-8000-000000000314', N'LCC26015', @prog_agr_dip, N'Enrolled', N'Noah Kavieng'),
        (N'c.kundiawa@lccb.ac.pg', N'00000000-0000-4000-8000-000000000315', N'LCC26016', @prog_agr_dip, N'Enrolled', N'Caleb Kundiawa'),
        (N'f.daru@lccb.ac.pg', N'00000000-0000-4000-8000-000000000316', N'LCC26017', @prog_agr_cert, N'Applied', N'Faith Daru'),
        (N'i.buka@lccb.ac.pg', N'00000000-0000-4000-8000-000000000317', N'LCC26018', @prog_agr_dip, N'Graduated', N'Isaac Buka');

    INSERT INTO dbo.users (entra_id, email, role, status)
    SELECT s.entra, s.email, N'Student', N'Active'
    FROM @stu s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.users u WHERE u.email = s.email);

    INSERT INTO dbo.students (student_id, student_number, programme_id, enrolment_status, emergency_contact)
    SELECT u.user_id, s.student_number, s.programme_id, s.enrolment_status, N'Next of kin — 675 7000 ' + RIGHT(s.student_number, 4)
    FROM @stu s
    JOIN dbo.users u ON u.email = s.email
    WHERE NOT EXISTS (SELECT 1 FROM dbo.students st WHERE st.student_id = u.user_id);

    /* Admissions (enrolment analytics): linked rows for the 18, plus extra reject/apply */
    INSERT INTO dbo.admissions (
        programme_id, applicant_name, applicant_email, applicant_phone, date_of_birth, gender,
        status, reviewed_by, decision_date, student_id, created_at
    )
    SELECT
        s.programme_id,
        s.applicant_name,
        s.email,
        N'675 7' + RIGHT(s.student_number, 6),
        DATEADD(YEAR, -20, CAST(N'2006-03-15' AS DATE)),
        CASE WHEN LEFT(s.applicant_name, 1) IN (N'M', N'J', N'P', N'D', N'T', N'B', N'N', N'C', N'I') THEN N'Male' ELSE N'Female' END,
        CASE s.enrolment_status
            WHEN N'Enrolled' THEN N'Approved'
            WHEN N'Graduated' THEN N'Approved'
            ELSE N'Applied'
        END,
        CASE WHEN s.enrolment_status IN (N'Enrolled', N'Graduated') THEN @registrar ELSE NULL END,
        CASE WHEN s.enrolment_status IN (N'Enrolled', N'Graduated') THEN CAST(N'2026-01-20' AS DATE) ELSE NULL END,
        u.user_id,
        CAST(N'2025-11-10' AS DATETIME2)
    FROM @stu s
    JOIN dbo.users u ON u.email = s.email
    WHERE NOT EXISTS (SELECT 1 FROM dbo.admissions a WHERE a.applicant_email = s.email);

    INSERT INTO dbo.admissions (
        programme_id, applicant_name, applicant_email, applicant_phone, status, reviewed_by, decision_date, created_at
    )
    SELECT v.programme_id, v.applicant_name, v.applicant_email, v.phone, v.status, @registrar, v.decision_date, v.created_at
    FROM (VALUES
        (@prog_bam_dip, N'Paul Kavieng', N'paul.kavieng.apply@gmail.com', N'675 72110001', N'Rejected', CAST(N'2026-01-22' AS DATE), CAST(N'2025-11-18' AS DATETIME2)),
        (@prog_bam_dip, N'Nancy Lae', N'nancy.lae.apply@gmail.com', N'675 72110002', N'Rejected', CAST(N'2026-01-22' AS DATE), CAST(N'2025-11-19' AS DATETIME2)),
        (@prog_min_dip, N'Mark Kerema', N'mark.kerema.apply@gmail.com', N'675 72110003', N'Rejected', CAST(N'2026-01-23' AS DATE), CAST(N'2025-11-20' AS DATETIME2)),
        (@prog_agr_dip, N'Rose Buka', N'rose.buka.apply@gmail.com', N'675 72110004', N'Rejected', CAST(N'2026-01-23' AS DATE), CAST(N'2025-11-21' AS DATETIME2)),
        (@prog_bam_cert, N'Eli Wabag', N'eli.wabag.apply@gmail.com', N'675 72110005', N'Applied', CAST(NULL AS DATE), CAST(N'2026-02-02' AS DATETIME2)),
        (@prog_min_cert, N'Joy Aitape', N'joy.aitape.apply@gmail.com', N'675 72110006', N'Under Review', CAST(NULL AS DATE), CAST(N'2026-02-03' AS DATETIME2)),
        (@prog_agr_cert, N'Silas Mendi', N'silas.mendi.apply@gmail.com', N'675 72110007', N'Applied', CAST(NULL AS DATE), CAST(N'2026-02-04' AS DATETIME2))
    ) AS v(programme_id, applicant_name, applicant_email, phone, status, decision_date, created_at)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.admissions a WHERE a.applicant_email = v.applicant_email);

    /* ----- Registrations: 3–5 courses for enrolled students, mix Approved / Pending ----- */

    DECLARE @reg_plan TABLE (
        student_number NVARCHAR(20) NOT NULL,
        course_code NVARCHAR(20) NOT NULL,
        status NVARCHAR(20) NOT NULL
    );
    INSERT INTO @reg_plan VALUES
        (N'LCC26001', N'BAM101', N'Approved'), (N'LCC26001', N'BAM102', N'Approved'),
        (N'LCC26001', N'BAM103', N'Approved'), (N'LCC26001', N'BAM104', N'Pending'),
        (N'LCC26002', N'BAM101', N'Approved'), (N'LCC26002', N'BAM102', N'Approved'),
        (N'LCC26002', N'BAM103', N'Approved'), (N'LCC26002', N'BAM104', N'Approved'),
        (N'LCC26002', N'BAM105', N'Pending'),
        (N'LCC26003', N'BAM101', N'Approved'), (N'LCC26003', N'BAM102', N'Approved'),
        (N'LCC26003', N'BAM103', N'Pending'),
        (N'LCC26004', N'BAM101', N'Approved'), (N'LCC26004', N'BAM102', N'Approved'),
        (N'LCC26004', N'BAM103', N'Approved'), (N'LCC26004', N'BAM105', N'Approved'),
        (N'LCC26005', N'BAM101', N'Approved'), (N'LCC26005', N'BAM102', N'Pending'),
        (N'LCC26005', N'BAM103', N'Approved'), (N'LCC26005', N'BAM104', N'Approved'),
        (N'LCC26005', N'BAM105', N'Pending'),
        (N'LCC26006', N'CBT101', N'Approved'), (N'LCC26006', N'CBT102', N'Approved'),
        (N'LCC26006', N'CBT103', N'Approved'), (N'LCC26006', N'CBT104', N'Pending'),
        (N'LCC26009', N'MIN101', N'Approved'), (N'LCC26009', N'MIN102', N'Approved'),
        (N'LCC26009', N'MIN103', N'Approved'), (N'LCC26009', N'MIN104', N'Pending'),
        (N'LCC26010', N'MIN101', N'Approved'), (N'LCC26010', N'MIN102', N'Approved'),
        (N'LCC26010', N'MIN103', N'Approved'), (N'LCC26010', N'MIN104', N'Approved'),
        (N'LCC26010', N'MIN105', N'Pending'),
        (N'LCC26011', N'MIN101', N'Approved'), (N'LCC26011', N'MIN102', N'Approved'),
        (N'LCC26011', N'MIN103', N'Pending'),
        (N'LCC26014', N'AGR101', N'Approved'), (N'LCC26014', N'AGR102', N'Approved'),
        (N'LCC26014', N'AGR103', N'Approved'), (N'LCC26014', N'AGR104', N'Pending'),
        (N'LCC26015', N'AGR101', N'Approved'), (N'LCC26015', N'AGR102', N'Approved'),
        (N'LCC26015', N'AGR103', N'Approved'), (N'LCC26015', N'AGR104', N'Approved'),
        (N'LCC26015', N'AGR105', N'Pending'),
        (N'LCC26016', N'AGR101', N'Approved'), (N'LCC26016', N'AGR102', N'Approved'),
        (N'LCC26016', N'AGR103', N'Pending');

    INSERT INTO dbo.registrations (student_id, allocation_id, attempt_no, status, approved_by, registered_at)
    SELECT
        st.student_id,
        ca.allocation_id,
        1,
        rp.status,
        CASE WHEN rp.status = N'Approved' THEN @registrar ELSE NULL END,
        CAST(N'2026-02-05' AS DATETIME2)
    FROM @reg_plan rp
    JOIN dbo.students st ON st.student_number = rp.student_number
    JOIN dbo.courses c ON c.course_code = rp.course_code AND c.programme_id = st.programme_id
    JOIN dbo.course_allocations ca
        ON ca.course_id = c.course_id AND ca.semester_id = @sem_id
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.registrations r
        WHERE r.student_id = st.student_id AND r.allocation_id = ca.allocation_id
    );

    /* ----- Attendance (sessions + Present/Absent/Late) ----- */

    INSERT INTO dbo.attendance_sessions (allocation_id, session_date)
    SELECT DISTINCT ca.allocation_id, d.session_date
    FROM dbo.registrations r
    JOIN dbo.course_allocations ca ON ca.allocation_id = r.allocation_id
    CROSS JOIN (VALUES
        (CAST(N'2026-02-10' AS DATE)),
        (CAST(N'2026-02-17' AS DATE)),
        (CAST(N'2026-02-24' AS DATE))
    ) AS d(session_date)
    WHERE r.status = N'Approved' AND ca.semester_id = @sem_id
      AND NOT EXISTS (
          SELECT 1 FROM dbo.attendance_sessions s
          WHERE s.allocation_id = ca.allocation_id AND s.session_date = d.session_date
      );

    INSERT INTO dbo.attendances (session_id, student_id, status)
    SELECT
        s.session_id,
        r.student_id,
        CASE (r.student_id + DATEPART(DAY, s.session_date)) % 5
            WHEN 0 THEN N'Absent'
            WHEN 1 THEN N'Late'
            ELSE N'Present'
        END
    FROM dbo.attendance_sessions s
    JOIN dbo.registrations r ON r.allocation_id = s.allocation_id AND r.status = N'Approved'
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.attendances a
        WHERE a.session_id = s.session_id AND a.student_id = r.student_id
    );

    /* ----- Assessments (40% + 60% = 100%) and published grades ----- */

    INSERT INTO dbo.assessments (allocation_id, title, weight_percent, max_marks)
    SELECT DISTINCT ca.allocation_id, N'Continuous Assessment', 40.00, 100.00
    FROM dbo.registrations r
    JOIN dbo.course_allocations ca ON ca.allocation_id = r.allocation_id
    WHERE r.status = N'Approved' AND ca.semester_id = @sem_id
      AND NOT EXISTS (
          SELECT 1 FROM dbo.assessments x
          WHERE x.allocation_id = ca.allocation_id AND x.title = N'Continuous Assessment'
      );

    INSERT INTO dbo.assessments (allocation_id, title, weight_percent, max_marks)
    SELECT DISTINCT ca.allocation_id, N'Final Examination', 60.00, 100.00
    FROM dbo.registrations r
    JOIN dbo.course_allocations ca ON ca.allocation_id = r.allocation_id
    WHERE r.status = N'Approved' AND ca.semester_id = @sem_id
      AND NOT EXISTS (
          SELECT 1 FROM dbo.assessments x
          WHERE x.allocation_id = ca.allocation_id AND x.title = N'Final Examination'
      );

    INSERT INTO dbo.grades (assessment_id, student_id, marks_obtained, grade_letter, published)
    SELECT
        a.assessment_id,
        r.student_id,
        CASE
            WHEN a.title = N'Continuous Assessment' THEN
                CASE st.student_number
                    WHEN N'LCC26001' THEN 88.00
                    WHEN N'LCC26002' THEN 76.00
                    WHEN N'LCC26003' THEN 68.00
                    WHEN N'LCC26004' THEN 82.00
                    WHEN N'LCC26005' THEN 71.00
                    WHEN N'LCC26006' THEN 79.00
                    WHEN N'LCC26009' THEN 85.00
                    WHEN N'LCC26010' THEN 73.00
                    WHEN N'LCC26011' THEN 64.00
                    WHEN N'LCC26014' THEN 81.00
                    WHEN N'LCC26015' THEN 70.00
                    WHEN N'LCC26016' THEN 58.00
                    ELSE 75.00
                END
            ELSE
                CASE st.student_number
                    WHEN N'LCC26001' THEN 84.00
                    WHEN N'LCC26002' THEN 72.00
                    WHEN N'LCC26003' THEN 66.00
                    WHEN N'LCC26004' THEN 90.00
                    WHEN N'LCC26005' THEN 74.00
                    WHEN N'LCC26006' THEN 77.00
                    WHEN N'LCC26009' THEN 80.00
                    WHEN N'LCC26010' THEN 71.00
                    WHEN N'LCC26011' THEN 62.00
                    WHEN N'LCC26014' THEN 78.00
                    WHEN N'LCC26015' THEN 69.00
                    WHEN N'LCC26016' THEN 55.00
                    ELSE 70.00
                END
        END,
        CASE
            WHEN a.title = N'Continuous Assessment' THEN
                CASE st.student_number
                    WHEN N'LCC26001' THEN N'A'
                    WHEN N'LCC26002' THEN N'B'
                    WHEN N'LCC26003' THEN N'C'
                    WHEN N'LCC26004' THEN N'A'
                    WHEN N'LCC26005' THEN N'B'
                    WHEN N'LCC26006' THEN N'B'
                    WHEN N'LCC26009' THEN N'A'
                    WHEN N'LCC26010' THEN N'B'
                    WHEN N'LCC26011' THEN N'C'
                    WHEN N'LCC26014' THEN N'A'
                    WHEN N'LCC26015' THEN N'B'
                    WHEN N'LCC26016' THEN N'D'
                    ELSE N'B'
                END
            ELSE
                CASE st.student_number
                    WHEN N'LCC26001' THEN N'A'
                    WHEN N'LCC26002' THEN N'B'
                    WHEN N'LCC26003' THEN N'C'
                    WHEN N'LCC26004' THEN N'A'
                    WHEN N'LCC26005' THEN N'B'
                    WHEN N'LCC26006' THEN N'B'
                    WHEN N'LCC26009' THEN N'A'
                    WHEN N'LCC26010' THEN N'B'
                    WHEN N'LCC26011' THEN N'C'
                    WHEN N'LCC26014' THEN N'B'
                    WHEN N'LCC26015' THEN N'C'
                    WHEN N'LCC26016' THEN N'D'
                    ELSE N'B'
                END
        END,
        1
    FROM dbo.assessments a
    JOIN dbo.registrations r ON r.allocation_id = a.allocation_id AND r.status = N'Approved'
    JOIN dbo.students st ON st.student_id = r.student_id
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.grades g
        WHERE g.assessment_id = a.assessment_id AND g.student_id = r.student_id
    );

    /* ----- Hostels / rooms / accommodation ----- */

    IF NOT EXISTS (SELECT 1 FROM dbo.hostels WHERE hostel_name = N'Melanesian House')
        INSERT INTO dbo.hostels (hostel_name) VALUES (N'Melanesian House');
    IF NOT EXISTS (SELECT 1 FROM dbo.hostels WHERE hostel_name = N'Pacific House')
        INSERT INTO dbo.hostels (hostel_name) VALUES (N'Pacific House');
    IF NOT EXISTS (SELECT 1 FROM dbo.hostels WHERE hostel_name = N'Highland House')
        INSERT INTO dbo.hostels (hostel_name) VALUES (N'Highland House');

    DECLARE @h_mel INT = (SELECT hostel_id FROM dbo.hostels WHERE hostel_name = N'Melanesian House');
    DECLARE @h_pac INT = (SELECT hostel_id FROM dbo.hostels WHERE hostel_name = N'Pacific House');
    DECLARE @h_hig INT = (SELECT hostel_id FROM dbo.hostels WHERE hostel_name = N'Highland House');

    DECLARE @rooms TABLE (hostel_id INT, room_number NVARCHAR(20), capacity INT);
    INSERT INTO @rooms VALUES
        (@h_mel, N'101', 2), (@h_mel, N'102', 2), (@h_mel, N'103', 2), (@h_mel, N'104', 2),
        (@h_pac, N'201', 2), (@h_pac, N'202', 2), (@h_pac, N'203', 2),
        (@h_hig, N'301', 2), (@h_hig, N'302', 2), (@h_hig, N'303', 2);

    INSERT INTO dbo.rooms (hostel_id, room_number, capacity)
    SELECT r.hostel_id, r.room_number, r.capacity
    FROM @rooms r
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.rooms x
        WHERE x.hostel_id = r.hostel_id AND x.room_number = r.room_number
    );

    DECLARE @room TABLE (student_number NVARCHAR(20), hostel_name NVARCHAR(100), room_number NVARCHAR(20), status NVARCHAR(15), allocated DATE, vacated DATE);
    INSERT INTO @room VALUES
        (N'LCC26001', N'Melanesian House', N'101', N'Active', '2026-02-01', NULL),
        (N'LCC26002', N'Melanesian House', N'101', N'Active', '2026-02-01', NULL),
        (N'LCC26003', N'Melanesian House', N'102', N'Active', '2026-02-01', NULL),
        (N'LCC26004', N'Pacific House', N'201', N'Active', '2026-02-02', NULL),
        (N'LCC26005', N'Pacific House', N'202', N'Active', '2026-02-02', NULL),
        (N'LCC26009', N'Highland House', N'301', N'Active', '2026-02-03', NULL),
        (N'LCC26010', N'Highland House', N'302', N'Active', '2026-02-03', NULL),
        (N'LCC26014', N'Melanesian House', N'103', N'Active', '2026-02-04', NULL),
        (N'LCC26008', N'Pacific House', N'203', N'Vacated', '2024-02-01', '2025-11-30'),
        (N'LCC26013', N'Highland House', N'303', N'Vacated', '2024-02-01', '2025-11-28');

    INSERT INTO dbo.accommodation_records (
        student_id, room_id, status, allocated_by, date_allocated, date_vacated
    )
    SELECT
        st.student_id,
        rm.room_id,
        p.status,
        @registrar,
        p.allocated,
        p.vacated
    FROM @room p
    JOIN dbo.students st ON st.student_number = p.student_number
    JOIN dbo.hostels h ON h.hostel_name = p.hostel_name
    JOIN dbo.rooms rm ON rm.hostel_id = h.hostel_id AND rm.room_number = p.room_number
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.accommodation_records ar
        WHERE ar.student_id = st.student_id
          AND ar.room_id = rm.room_id
          AND ar.status = p.status
    );

    /* ----- Welfare (5 cases, 2–3 notes each) ----- */

    IF NOT EXISTS (SELECT 1 FROM dbo.welfare_cases wc
                   JOIN dbo.students st ON st.student_id = wc.student_id
                   WHERE st.student_number = N'LCC26002' AND wc.case_type = N'Financial hardship')
        INSERT INTO dbo.welfare_cases (student_id, assigned_officer_id, case_type, status, date_logged, date_resolved)
        SELECT student_id, @registrar, N'Financial hardship', N'Open', '2026-03-02', NULL
        FROM dbo.students WHERE student_number = N'LCC26002';

    IF NOT EXISTS (SELECT 1 FROM dbo.welfare_cases wc
                   JOIN dbo.students st ON st.student_id = wc.student_id
                   WHERE st.student_number = N'LCC26005' AND wc.case_type = N'Family support')
        INSERT INTO dbo.welfare_cases (student_id, assigned_officer_id, case_type, status, date_logged, date_resolved)
        SELECT student_id, @hod_bus, N'Family support', N'Open', '2026-03-08', NULL
        FROM dbo.students WHERE student_number = N'LCC26005';

    IF NOT EXISTS (SELECT 1 FROM dbo.welfare_cases wc
                   JOIN dbo.students st ON st.student_id = wc.student_id
                   WHERE st.student_number = N'LCC26009' AND wc.case_type = N'Health')
        INSERT INTO dbo.welfare_cases (student_id, assigned_officer_id, case_type, status, date_logged, date_resolved)
        SELECT student_id, @registrar, N'Health', N'In Progress', '2026-02-20', NULL
        FROM dbo.students WHERE student_number = N'LCC26009';

    IF NOT EXISTS (SELECT 1 FROM dbo.welfare_cases wc
                   JOIN dbo.students st ON st.student_id = wc.student_id
                   WHERE st.student_number = N'LCC26014' AND wc.case_type = N'Academic')
        INSERT INTO dbo.welfare_cases (student_id, assigned_officer_id, case_type, status, date_logged, date_resolved)
        SELECT student_id, @hod_agr, N'Academic', N'In Progress', '2026-03-01', NULL
        FROM dbo.students WHERE student_number = N'LCC26014';

    IF NOT EXISTS (SELECT 1 FROM dbo.welfare_cases wc
                   JOIN dbo.students st ON st.student_id = wc.student_id
                   WHERE st.student_number = N'LCC26003' AND wc.case_type = N'Accommodation')
        INSERT INTO dbo.welfare_cases (student_id, assigned_officer_id, case_type, status, date_logged, date_resolved)
        SELECT student_id, @registrar, N'Accommodation', N'Resolved', '2026-02-12', '2026-03-05'
        FROM dbo.students WHERE student_number = N'LCC26003';

    INSERT INTO dbo.case_notes (case_id, staff_id, note, created_at)
    SELECT wc.case_id, wc.assigned_officer_id, n.note, n.created_at
    FROM dbo.welfare_cases wc
    JOIN dbo.students st ON st.student_id = wc.student_id
    JOIN (VALUES
        (N'LCC26002', N'Financial hardship', N'Student reported delayed family support for fees.', CAST(N'2026-03-02T09:00:00' AS DATETIME2)),
        (N'LCC26002', N'Financial hardship', N'Registrar requested a fee-plan discussion this week.', CAST(N'2026-03-03T11:20:00' AS DATETIME2)),
        (N'LCC26005', N'Family support', N'Family illness in Eastern Highlands; student requested leave advice.', CAST(N'2026-03-08T08:40:00' AS DATETIME2)),
        (N'LCC26005', N'Family support', N'HoD contacted next of kin; follow-up after weekend.', CAST(N'2026-03-09T14:10:00' AS DATETIME2)),
        (N'LCC26005', N'Family support', N'Short absence approved pending medical note.', CAST(N'2026-03-10T10:00:00' AS DATETIME2)),
        (N'LCC26009', N'Health', N'Reported malaria-like symptoms; referred to clinic.', CAST(N'2026-02-20T16:00:00' AS DATETIME2)),
        (N'LCC26009', N'Health', N'Clinic confirmed treatment; light duties for one week.', CAST(N'2026-02-22T09:30:00' AS DATETIME2)),
        (N'LCC26014', N'Academic', N'Struggling with Farm Business calculations; extra tutorial offered.', CAST(N'2026-03-01T13:00:00' AS DATETIME2)),
        (N'LCC26014', N'Academic', N'Attended tutorial; lecturer will re-check assignment draft.', CAST(N'2026-03-04T15:45:00' AS DATETIME2)),
        (N'LCC26003', N'Accommodation', N'Room 102 tap leaking; reported to hostel supervisor.', CAST(N'2026-02-12T08:15:00' AS DATETIME2)),
        (N'LCC26003', N'Accommodation', N'Plumber attended; leak repaired.', CAST(N'2026-02-14T12:00:00' AS DATETIME2)),
        (N'LCC26003', N'Accommodation', N'Student confirmed issue resolved; case closed.', CAST(N'2026-03-05T09:00:00' AS DATETIME2))
    ) AS n(student_number, case_type, note, created_at)
      ON n.student_number = st.student_number AND n.case_type = wc.case_type
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.case_notes cn
        WHERE cn.case_id = wc.case_id AND cn.note = n.note
    );

    /* ----- Notices ----- */

    INSERT INTO dbo.notices (author_id, title, content, target_role, posted_at)
    SELECT @registrar, v.title, v.content, v.target_role, v.posted_at
    FROM (VALUES
        (N'Semester Registration Opens',
         N'Semester 1, 2026 registration is open. Enrolled students must complete unit registration by 13 February 2026.',
         N'Student', CAST(N'2026-01-28T08:00:00' AS DATETIME2)),
        (N'Mid-Semester Exam Schedule',
         N'Mid-semester examinations run 23–27 March 2026. See your lecturer for venue details.',
         NULL, CAST(N'2026-03-02T09:00:00' AS DATETIME2)),
        (N'Hostel Inspection Notice',
         N'Hostel inspections will take place on 18 March 2026. Rooms must be accessible from 9:00 a.m.',
         N'Student', CAST(N'2026-03-10T10:00:00' AS DATETIME2)),
        (N'Faculty Meeting',
         N'Heads of Department and lecturers are asked to attend the faculty meeting on 20 March 2026, 2:00 p.m., administration conference room.',
         N'Lecturer', CAST(N'2026-03-12T11:00:00' AS DATETIME2)),
        (N'Graduation Preparation Notice',
         N'Graduation rehearsal is scheduled for 6 November 2026. Graduands will receive a checklist from the Registrar.',
         NULL, CAST(N'2026-04-01T08:30:00' AS DATETIME2))
    ) AS v(title, content, target_role, posted_at)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.notices n WHERE n.title = v.title);

COMMIT TRANSACTION;

PRINT N'Demo seed committed. Restart the API to hash lab passwords on new users.';

SELECT N'Staff' AS bucket, role, COUNT(*) AS n FROM dbo.users u
JOIN dbo.staff s ON s.staff_id = u.user_id
GROUP BY role
UNION ALL
SELECT N'Students', enrolment_status, COUNT(*) FROM dbo.students GROUP BY enrolment_status
UNION ALL
SELECT N'Registrations', status, COUNT(*) FROM dbo.registrations GROUP BY status
UNION ALL
SELECT N'Accommodation', status, COUNT(*) FROM dbo.accommodation_records GROUP BY status
UNION ALL
SELECT N'Welfare', status, COUNT(*) FROM dbo.welfare_cases GROUP BY status
UNION ALL
SELECT N'Notices', N'all', COUNT(*) FROM dbo.notices
UNION ALL
SELECT N'Admissions', status, COUNT(*) FROM dbo.admissions GROUP BY status
ORDER BY 1, 2;

GO

/* Login (after API restart, password LccCms!2026):
     principal@lccb.ac.pg
     registrar@lccb.ac.pg
     hod@lccb.ac.pg                 Business
     hod.ministry@lccb.ac.pg
     hod.agriculture@lccb.ac.pg
     lecturer@lccb.ac.pg            Business
     lecturer.kila@lccb.ac.pg       Business
     lecturer.ministry@lccb.ac.pg
     lecturer.agriculture@lccb.ac.pg
     student@lccb.ac.pg             LCC26001 (and other *@lccb.ac.pg students)
*/

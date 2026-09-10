/* =========================================================================
   LCC-CMS — align academic structure with LCCB faculties (insert/update)
   Date: 2026-09-11
   Target: LCCCMSDB (already seeded)

   Preserves schema, existing IDs, students, courses, allocations, staff.
   Does not DELETE or DROP.

   Typical ID mapping after original Dev_Seed:
     faculty_id 1  Applied Studies  → renamed Faculty of Business Studies
     department_id 1  Department of Business Administration (unchanged)
     programme_id 1   Diploma in BAM (unchanged)
     hod@lccb.ac.pg remains staff.department_id = Business Administration
   ========================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.faculties', N'U') IS NULL
    THROW 50041, N'Abort: faculties table missing. Run LCC_CMS_Schema.sql first.', 1;

BEGIN TRANSACTION;

    /* ----- Faculty 1: rename Applied Studies → Business Studies (keep faculty_id) ----- */

    UPDATE dbo.faculties
    SET faculty_name = N'Faculty of Business Studies'
    WHERE faculty_name = N'Faculty of Applied Studies'
      AND NOT EXISTS (
          SELECT 1 FROM dbo.faculties WHERE faculty_name = N'Faculty of Business Studies'
      );

    IF NOT EXISTS (SELECT 1 FROM dbo.faculties WHERE faculty_name = N'Faculty of Business Studies')
        INSERT INTO dbo.faculties (faculty_name)
        VALUES (N'Faculty of Business Studies');

    DECLARE @fac_business INT =
        (SELECT faculty_id FROM dbo.faculties WHERE faculty_name = N'Faculty of Business Studies');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.departments
        WHERE faculty_id = @fac_business
          AND department_name = N'Department of Business Administration'
    )
        INSERT INTO dbo.departments (faculty_id, department_name)
        VALUES (@fac_business, N'Department of Business Administration');

    DECLARE @dept_business INT =
        (SELECT department_id FROM dbo.departments
         WHERE faculty_id = @fac_business
           AND department_name = N'Department of Business Administration');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.programmes
        WHERE department_id = @dept_business
          AND programme_name = N'Diploma in Business Administration and Management'
    )
        INSERT INTO dbo.programmes (department_id, programme_name, duration_years)
        VALUES (@dept_business, N'Diploma in Business Administration and Management', 3.0);

    IF NOT EXISTS (
        SELECT 1 FROM dbo.programmes
        WHERE department_id = @dept_business
          AND programme_name = N'Certificate in Business Administration and Management'
    )
        INSERT INTO dbo.programmes (department_id, programme_name, duration_years)
        VALUES (@dept_business, N'Certificate in Business Administration and Management', 1.0);

    /* ----- Faculty 2: Ministry ----- */

    IF NOT EXISTS (SELECT 1 FROM dbo.faculties WHERE faculty_name = N'Faculty of Ministry Studies')
        INSERT INTO dbo.faculties (faculty_name)
        VALUES (N'Faculty of Ministry Studies');

    DECLARE @fac_ministry INT =
        (SELECT faculty_id FROM dbo.faculties WHERE faculty_name = N'Faculty of Ministry Studies');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.departments
        WHERE faculty_id = @fac_ministry
          AND department_name = N'Department of Applied Ministry'
    )
        INSERT INTO dbo.departments (faculty_id, department_name)
        VALUES (@fac_ministry, N'Department of Applied Ministry');

    DECLARE @dept_ministry INT =
        (SELECT department_id FROM dbo.departments
         WHERE faculty_id = @fac_ministry
           AND department_name = N'Department of Applied Ministry');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.programmes
        WHERE department_id = @dept_ministry
          AND programme_name = N'Diploma in Applied Ministry'
    )
        INSERT INTO dbo.programmes (department_id, programme_name, duration_years)
        VALUES (@dept_ministry, N'Diploma in Applied Ministry', 3.0);

    IF NOT EXISTS (
        SELECT 1 FROM dbo.programmes
        WHERE department_id = @dept_ministry
          AND programme_name = N'Certificate in Applied Ministry'
    )
        INSERT INTO dbo.programmes (department_id, programme_name, duration_years)
        VALUES (@dept_ministry, N'Certificate in Applied Ministry', 1.0);

    /* ----- Faculty 3: Agriculture ----- */

    IF NOT EXISTS (SELECT 1 FROM dbo.faculties WHERE faculty_name = N'Faculty of Agricultural Studies')
        INSERT INTO dbo.faculties (faculty_name)
        VALUES (N'Faculty of Agricultural Studies');

    DECLARE @fac_agri INT =
        (SELECT faculty_id FROM dbo.faculties WHERE faculty_name = N'Faculty of Agricultural Studies');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.departments
        WHERE faculty_id = @fac_agri
          AND department_name = N'Department of Tropical Agriculture'
    )
        INSERT INTO dbo.departments (faculty_id, department_name)
        VALUES (@fac_agri, N'Department of Tropical Agriculture');

    DECLARE @dept_agri INT =
        (SELECT department_id FROM dbo.departments
         WHERE faculty_id = @fac_agri
           AND department_name = N'Department of Tropical Agriculture');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.programmes
        WHERE department_id = @dept_agri
          AND programme_name = N'Diploma in Tropical Agriculture'
    )
        INSERT INTO dbo.programmes (department_id, programme_name, duration_years)
        VALUES (@dept_agri, N'Diploma in Tropical Agriculture', 3.0);

    IF NOT EXISTS (
        SELECT 1 FROM dbo.programmes
        WHERE department_id = @dept_agri
          AND programme_name = N'Certificate in Tropical Agriculture'
    )
        INSERT INTO dbo.programmes (department_id, programme_name, duration_years)
        VALUES (@dept_agri, N'Certificate in Tropical Agriculture', 1.0);

    /* Keep the seeded HoD on Business Administration (do not move or delete). */
    UPDATE dbo.staff
    SET department_id = @dept_business
    WHERE staff_id = (SELECT user_id FROM dbo.users WHERE email = N'hod@lccb.ac.pg')
      AND EXISTS (SELECT 1 FROM dbo.users WHERE email = N'hod@lccb.ac.pg');

COMMIT TRANSACTION;

PRINT N'LCCB academic structure aligned (insert/update only).';

SELECT f.faculty_id, f.faculty_name, d.department_id, d.department_name,
       p.programme_id, p.programme_name, p.duration_years
FROM dbo.faculties f
JOIN dbo.departments d ON d.faculty_id = f.faculty_id
JOIN dbo.programmes p ON p.department_id = d.department_id
ORDER BY f.faculty_name, p.programme_name;

SELECT u.email, s.job_title, s.department_id, d.department_name
FROM dbo.users u
JOIN dbo.staff s ON s.staff_id = u.user_id
LEFT JOIN dbo.departments d ON d.department_id = s.department_id
WHERE u.role = N'HoD';

GO

-- LCC-CMS schema upgrade Rev 8 — students.full_name + postal_address
-- Idempotent. Backfills full_name from admissions.applicant_name, then email local-part.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.students', N'U') IS NULL
    THROW 50081, N'Rev8 abort: dbo.students is missing.', 1;
GO

IF COL_LENGTH(N'dbo.students', N'full_name') IS NULL
BEGIN
    ALTER TABLE dbo.students ADD full_name NVARCHAR(150) NULL;
END;
GO

IF COL_LENGTH(N'dbo.students', N'postal_address') IS NULL
BEGIN
    ALTER TABLE dbo.students ADD postal_address NVARCHAR(500) NULL;
END;
GO

UPDATE s
SET full_name = COALESCE(
        NULLIF(LTRIM(RTRIM(s.full_name)), N''),
        NULLIF(LTRIM(RTRIM(a.applicant_name)), N''),
        NULLIF(LEFT(u.email, NULLIF(CHARINDEX(N'@', u.email), 0) - 1), N''),
        s.student_number)
FROM dbo.students AS s
INNER JOIN dbo.users AS u ON u.user_id = s.student_id
LEFT JOIN dbo.admissions AS a ON a.student_id = s.student_id
WHERE s.full_name IS NULL OR LTRIM(RTRIM(s.full_name)) = N'';
GO

IF EXISTS (
    SELECT 1 FROM dbo.students
    WHERE full_name IS NULL OR LTRIM(RTRIM(full_name)) = N'')
    THROW 50082, N'Rev8 abort: could not backfill full_name for every student.', 1;
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.students')
      AND name = N'full_name'
      AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.students ALTER COLUMN full_name NVARCHAR(150) NOT NULL;
END;
GO

SELECT
    COL_LENGTH(N'dbo.students', N'full_name') AS full_name,
    COL_LENGTH(N'dbo.students', N'postal_address') AS postal_address;
GO

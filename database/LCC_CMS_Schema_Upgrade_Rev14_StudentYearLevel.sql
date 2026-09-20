/* Rev 14 — student year level and demo location fields.
   Preserves existing students rows. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.students', N'U') IS NULL
    THROW 50140, N'Rev14 abort: dbo.students is missing.', 1;
GO

IF COL_LENGTH(N'dbo.students', N'year_level') IS NULL
BEGIN
    ALTER TABLE dbo.students ADD year_level TINYINT NULL;
END;
GO

IF COL_LENGTH(N'dbo.students', N'year_level') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE name = N'CK_students_year_level' AND parent_object_id = OBJECT_ID(N'dbo.students'))
BEGIN
    ALTER TABLE dbo.students ADD CONSTRAINT CK_students_year_level
        CHECK (year_level IS NULL OR year_level BETWEEN 1 AND 3);
END;
GO

IF COL_LENGTH(N'dbo.students', N'province') IS NULL
BEGIN
    ALTER TABLE dbo.students ADD province NVARCHAR(80) NULL;
END;
GO

IF COL_LENGTH(N'dbo.students', N'district') IS NULL
BEGIN
    ALTER TABLE dbo.students ADD district NVARCHAR(80) NULL;
END;
GO

IF COL_LENGTH(N'dbo.students', N'village') IS NULL
BEGIN
    ALTER TABLE dbo.students ADD village NVARCHAR(80) NULL;
END;
GO

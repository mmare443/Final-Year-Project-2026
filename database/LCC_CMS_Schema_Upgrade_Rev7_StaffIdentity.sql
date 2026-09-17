-- LCC-CMS schema upgrade Rev 7 — staff_number (STF-YYYY-NNN) + full_name
-- Idempotent. Does not change PK staff_id (= users.user_id).
-- Batched with GO so newly added columns are visible to later statements.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.staff', N'U') IS NULL
    THROW 50001, N'Rev7 abort: dbo.staff is missing.', 1;
GO

IF COL_LENGTH(N'dbo.staff', N'staff_number') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD staff_number NVARCHAR(20) NULL;
END;
GO

IF COL_LENGTH(N'dbo.staff', N'full_name') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD full_name NVARCHAR(150) NULL;
END;
GO

-- Backfill display names from email local-part when empty.
UPDATE s
SET full_name = COALESCE(
        NULLIF(LTRIM(RTRIM(s.full_name)), N''),
        NULLIF(LEFT(u.email, NULLIF(CHARINDEX(N'@', u.email), 0) - 1), N''),
        CONCAT(N'Staff ', s.staff_id))
FROM dbo.staff AS s
INNER JOIN dbo.users AS u ON u.user_id = s.staff_id
WHERE s.full_name IS NULL OR LTRIM(RTRIM(s.full_name)) = N'';
GO

-- Backfill STF-{year}-NNN in staff_id order, continuing after any existing sequence.
DECLARE @year INT = YEAR(SYSUTCDATETIME());
DECLARE @prefix NVARCHAR(12) = CONCAT(N'STF-', @year, N'-');
DECLARE @start INT = 0;

SELECT @start = ISNULL(MAX(TRY_CONVERT(INT, RIGHT(staff_number, 3))), 0)
FROM dbo.staff
WHERE staff_number LIKE @prefix + N'[0-9][0-9][0-9]';

;WITH numbered AS (
    SELECT
        s.staff_id,
        ROW_NUMBER() OVER (ORDER BY s.staff_id) AS seq
    FROM dbo.staff AS s
    WHERE s.staff_number IS NULL OR LTRIM(RTRIM(s.staff_number)) = N''
)
UPDATE s
SET staff_number = CONCAT(@prefix, RIGHT(CONCAT(N'000', n.seq + @start), 3))
FROM dbo.staff AS s
INNER JOIN numbered AS n ON n.staff_id = s.staff_id;
GO

IF EXISTS (
    SELECT 1 FROM dbo.staff
    WHERE staff_number IS NULL OR LTRIM(RTRIM(staff_number)) = N''
       OR full_name IS NULL OR LTRIM(RTRIM(full_name)) = N'')
    THROW 50002, N'Rev7 abort: could not backfill staff_number/full_name for every row.', 1;
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.staff')
      AND name = N'staff_number'
      AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.staff ALTER COLUMN staff_number NVARCHAR(20) NOT NULL;
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.staff')
      AND name = N'full_name'
      AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.staff ALTER COLUMN full_name NVARCHAR(150) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_staff_staff_number' AND parent_object_id = OBJECT_ID(N'dbo.staff'))
BEGIN
    ALTER TABLE dbo.staff
        ADD CONSTRAINT CK_staff_staff_number
        CHECK (staff_number LIKE N'STF-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9]');
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_staff_staff_number' AND object_id = OBJECT_ID(N'dbo.staff'))
BEGIN
    CREATE UNIQUE INDEX UX_staff_staff_number ON dbo.staff(staff_number);
END;
GO

SELECT
    N'staff.staff_number' AS column_name,
    COL_LENGTH(N'dbo.staff', N'staff_number') AS col_length
UNION ALL
SELECT N'staff.full_name', COL_LENGTH(N'dbo.staff', N'full_name');
GO

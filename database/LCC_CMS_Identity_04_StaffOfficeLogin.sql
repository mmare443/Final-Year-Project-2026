/* Staff ID and office login codes.
   users.login_code is added first (nullable, no values written).
   Staff-number and login-code UPDATEs run only when @Apply = 1.
   Existing staff_number values are never overwritten.
   Blank staff numbers, if any, receive the next STF-YYYY-NNN value
   (for example STF-2026-001), which is what CK_staff_staff_number allows. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.users', N'login_code') IS NULL
    ALTER TABLE dbo.users ADD login_code NVARCHAR(30) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_users_login_code' AND object_id = OBJECT_ID(N'dbo.users'))
    CREATE UNIQUE INDEX UX_users_login_code ON dbo.users(login_code) WHERE login_code IS NOT NULL;
GO

SELECT
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS AS c
WHERE c.TABLE_SCHEMA = N'dbo'
  AND c.TABLE_NAME = N'staff'
  AND c.COLUMN_NAME IN (N'staff_id', N'staff_number');

SELECT
    cc.name AS ConstraintName,
    cc.definition AS ConstraintDefinition
FROM sys.check_constraints AS cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.staff')
  AND cc.name = N'CK_staff_staff_number';

SELECT
    s.staff_id AS ID,
    s.full_name AS Name,
    s.staff_number AS StaffNumber,
    u.email AS Email,
    u.role AS Role,
    u.status AS Status
FROM dbo.staff AS s
INNER JOIN dbo.users AS u ON u.user_id = s.staff_id
ORDER BY s.staff_id;

SELECT
    u.user_id AS ID,
    u.email AS Email,
    u.role AS Role,
    u.status AS Status,
    u.login_code AS LoginCode
FROM dbo.users AS u
WHERE u.email IN (
    N'registration@lccbportal.org',
    N'admissions@lccbportal.org',
    N'principal@lccbportal.org',
    N'finance@lccbportal.org')
ORDER BY u.email;
GO

DECLARE @Apply bit = 0;
DECLARE @Prefix nvarchar(12) = CONCAT(N'STF-', YEAR(SYSUTCDATETIME()), N'-');
DECLARE @Start int;

SELECT @Start = ISNULL(MAX(TRY_CONVERT(int, RIGHT(staff_number, 3))), 0)
FROM dbo.staff
WHERE staff_number LIKE @Prefix + N'[0-9][0-9][0-9]';

SELECT
    s.staff_id AS ID,
    s.full_name AS Name,
    s.staff_number AS CurrentStaffNumber,
    CONCAT(@Prefix, RIGHT(CONCAT(N'000', n.seq + @Start), 3)) AS ProposedStaffNumber
FROM dbo.staff AS s
INNER JOIN (
    SELECT staff_id, ROW_NUMBER() OVER (ORDER BY staff_id) AS seq
    FROM dbo.staff
    WHERE staff_number IS NULL OR LTRIM(RTRIM(staff_number)) = N''
) AS n ON n.staff_id = s.staff_id;

SELECT
    m.email AS Email,
    m.login_code AS LoginCode,
    u.user_id AS UserId
FROM (VALUES
    (N'registration@lccbportal.org', N'OFC-REGISTRAR'),
    (N'admissions@lccbportal.org', N'OFC-ADMISSIONS'),
    (N'principal@lccbportal.org', N'OFC-PRINCIPAL'),
    (N'finance@lccbportal.org', N'OFC-FINANCE')
) AS m(email, login_code)
LEFT JOIN dbo.users AS u ON u.email = m.email;

IF @Apply = 0
BEGIN
    PRINT N'Preview only. Existing staff numbers were not changed. Set @Apply = 1 to fill blanks and set office login codes.';
    RETURN;
END

;WITH numbered AS (
    SELECT staff_id, ROW_NUMBER() OVER (ORDER BY staff_id) AS seq
    FROM dbo.staff
    WHERE staff_number IS NULL OR LTRIM(RTRIM(staff_number)) = N''
)
UPDATE s
SET staff_number = CONCAT(@Prefix, RIGHT(CONCAT(N'000', n.seq + @Start), 3))
FROM dbo.staff AS s
INNER JOIN numbered AS n ON n.staff_id = s.staff_id;

UPDATE u
SET login_code = m.login_code
FROM dbo.users AS u
INNER JOIN (VALUES
    (N'registration@lccbportal.org', N'OFC-REGISTRAR'),
    (N'admissions@lccbportal.org', N'OFC-ADMISSIONS'),
    (N'principal@lccbportal.org', N'OFC-PRINCIPAL'),
    (N'finance@lccbportal.org', N'OFC-FINANCE')
) AS m(email, login_code) ON u.email = m.email
WHERE u.login_code IS NULL OR u.login_code = m.login_code;

PRINT N'Staff blanks filled. Office login codes set where those users exist. Existing staff numbers were left unchanged.';

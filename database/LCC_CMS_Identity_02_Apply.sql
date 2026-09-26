/* Identity assignment and temporary-password reset.
   Default is preview only. Data changes run only when @Apply = 1.
   Temporary password: LccCms!2026
   Hash is ASP.NET Identity PasswordHasher V3 for that password.
   must_change_password is the force-password-change flag. No second column exists.
   Run database/LCC_CMS_Identity_01_Audit.sql first. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.fn_lcc_letters', N'FN') IS NULL
    EXEC(N'
CREATE FUNCTION dbo.fn_lcc_letters(@value nvarchar(150))
RETURNS nvarchar(150)
AS
BEGIN
    DECLARE @i int = 1, @out nvarchar(150) = N'''';
    WHILE @i <= LEN(@value)
    BEGIN
        IF SUBSTRING(@value, @i, 1) LIKE N''[A-Za-z]''
            SET @out += SUBSTRING(@value, @i, 1);
        SET @i += 1;
    END
    RETURN LOWER(@out);
END');
GO

DECLARE @Apply bit = 0;
DECLARE @PasswordHash nvarchar(500) =
    N'AQAAAAIAAYagAAAAEFOjrzhqUCXTfUaEoRRth3WZA6m+cYidyw4wosGEelIO1WIoTYlUHzf6kiHFulxl0Q==';

IF OBJECT_ID(N'tempdb..#plan') IS NOT NULL DROP TABLE #plan;
CREATE TABLE #plan (
    user_id int NOT NULL PRIMARY KEY,
    account_kind nvarchar(20) NOT NULL,
    name nvarchar(150) NOT NULL,
    role nvarchar(30) NOT NULL,
    username nvarchar(255) NOT NULL,
    old_email nvarchar(255) NULL,
    new_email nvarchar(255) NULL,
    status nvarchar(20) NOT NULL,
    conflict nvarchar(200) NULL
);

INSERT INTO #plan (user_id, account_kind, name, role, username, old_email, new_email, status)
SELECT
    u.user_id,
    N'Student',
    st.full_name,
    u.role,
    st.student_number,
    u.email,
    LOWER(REPLACE(st.student_number, N'-', N'')) + N'@student.lccbportal.org',
    u.status
FROM dbo.users AS u
INNER JOIN dbo.students AS st ON st.student_id = u.user_id;

IF OBJECT_ID(N'tempdb..#staff_base') IS NOT NULL DROP TABLE #staff_base;
SELECT
    u.user_id,
    sf.full_name,
    u.role,
    u.email AS old_email,
    u.status,
    LOWER(
        LEFT(dbo.fn_lcc_letters(
            CASE WHEN CHARINDEX(N' ', LTRIM(RTRIM(sf.full_name))) = 0
                 THEN LTRIM(RTRIM(sf.full_name))
                 ELSE LEFT(LTRIM(RTRIM(sf.full_name)), CHARINDEX(N' ', LTRIM(RTRIM(sf.full_name))) - 1)
            END), 1)
        + dbo.fn_lcc_letters(
            CASE WHEN CHARINDEX(N' ', REVERSE(LTRIM(RTRIM(sf.full_name)))) = 0
                 THEN LTRIM(RTRIM(sf.full_name))
                 ELSE RIGHT(LTRIM(RTRIM(sf.full_name)), CHARINDEX(N' ', REVERSE(LTRIM(RTRIM(sf.full_name)))) - 1)
            END)
    ) AS base_local
INTO #staff_base
FROM dbo.users AS u
INNER JOIN dbo.staff AS sf ON sf.staff_id = u.user_id
WHERE u.email NOT IN (
    N'registration@lccbportal.org',
    N'admissions@lccbportal.org',
    N'principal@lccbportal.org',
    N'finance@lccbportal.org',
    N'noreply@lccbportal.org')
  AND NOT EXISTS (SELECT 1 FROM #plan AS p WHERE p.user_id = u.user_id);

DECLARE @uid int, @base nvarchar(80), @n int, @candidate nvarchar(255);
DECLARE staff_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT user_id, base_local FROM #staff_base ORDER BY base_local, user_id;
OPEN staff_cursor;
FETCH NEXT FROM staff_cursor INTO @uid, @base;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF @base IS NULL OR LEN(@base) < 2
    BEGIN
        INSERT INTO #plan (user_id, account_kind, name, role, username, old_email, new_email, status, conflict)
        SELECT @uid, N'Staff', full_name, role, old_email, old_email, NULL, status,
               N'Name does not produce a first initial and surname.'
        FROM #staff_base WHERE user_id = @uid;
    END
    ELSE
    BEGIN
        SET @n = 0;
        WHILE 1 = 1
        BEGIN
            SET @candidate = @base
                + CASE WHEN @n = 0 THEN N'' ELSE CAST(@n AS nvarchar(10)) END
                + N'@lccbportal.org';
            IF NOT EXISTS (SELECT 1 FROM #plan WHERE new_email = @candidate)
               AND NOT EXISTS (
                    SELECT 1 FROM dbo.users AS u
                    WHERE u.email = @candidate
                      AND u.user_id <> @uid
                      AND NOT EXISTS (
                          SELECT 1 FROM #plan AS moving
                          WHERE moving.user_id = u.user_id
                            AND moving.new_email IS NOT NULL
                            AND moving.new_email <> @candidate))
                BREAK;
            SET @n += 1;
            IF @n > 50 BREAK;
        END

        INSERT INTO #plan (user_id, account_kind, name, role, username, old_email, new_email, status)
        SELECT @uid, N'Staff', full_name, role, @candidate, old_email, @candidate, status
        FROM #staff_base WHERE user_id = @uid;
    END
    FETCH NEXT FROM staff_cursor INTO @uid, @base;
END
CLOSE staff_cursor;
DEALLOCATE staff_cursor;

UPDATE p
SET conflict = N'Proposed email is already used or duplicated.'
FROM #plan AS p
WHERE p.new_email IS NOT NULL
  AND (
      EXISTS (
          SELECT 1 FROM #plan AS other
          WHERE other.new_email = p.new_email AND other.user_id < p.user_id)
      OR EXISTS (
          SELECT 1 FROM dbo.users AS u
          WHERE u.email = p.new_email
            AND u.user_id <> p.user_id
            AND NOT EXISTS (
                SELECT 1 FROM #plan AS moving
                WHERE moving.user_id = u.user_id
                  AND moving.new_email IS NOT NULL
                  AND moving.new_email <> p.new_email))
  );

SELECT
    user_id AS ID,
    name AS Name,
    role AS Role,
    username AS Username,
    new_email AS Email,
    CASE WHEN status = N'Active' THEN 1 ELSE 0 END AS Active,
    old_email AS CurrentEmail,
    conflict AS Conflict
FROM #plan
ORDER BY account_kind, name, user_id;

SELECT
    SUM(CASE WHEN account_kind = N'Student' THEN 1 ELSE 0 END) AS TotalStudents,
    SUM(CASE WHEN account_kind = N'Staff' THEN 1 ELSE 0 END) AS TotalStaff,
    (SELECT COUNT(*) FROM (VALUES
        (N'registration@lccbportal.org'),
        (N'admissions@lccbportal.org'),
        (N'principal@lccbportal.org'),
        (N'finance@lccbportal.org'),
        (N'noreply@lccbportal.org')) AS office(email)
      WHERE EXISTS (SELECT 1 FROM dbo.users AS u WHERE u.email = office.email)
         OR EXISTS (SELECT 1 FROM #plan AS p WHERE p.new_email = office.email)
    ) AS TotalOfficeAccountsIncludingPlanned,
    CASE
        WHEN SUM(CASE WHEN conflict IS NOT NULL THEN 1 ELSE 0 END) > 0 THEN 0
        ELSE (
            SELECT COUNT(*) FROM dbo.users WHERE status = N'Active')
            + (
                SELECT COUNT(*)
                FROM (VALUES
                    (N'registration@lccbportal.org'),
                    (N'admissions@lccbportal.org'),
                    (N'principal@lccbportal.org'),
                    (N'finance@lccbportal.org'),
                    (N'noreply@lccbportal.org')) AS office(email)
                WHERE NOT EXISTS (SELECT 1 FROM dbo.users AS u WHERE u.email = office.email))
    END AS AccountsThatWouldBeReset,
    SUM(CASE WHEN new_email IS NULL OR LTRIM(RTRIM(new_email)) = N'' THEN 1 ELSE 0 END) AS AccountsMissingEmail,
    SUM(CASE WHEN conflict IS NOT NULL THEN 1 ELSE 0 END) AS DuplicateOrInvalidIdentities
FROM #plan;

IF @Apply = 0
BEGIN
    PRINT N'Preview only. Set @Apply = 1 in this script to write emails and reset active passwords.';
    IF OBJECT_ID(N'dbo.fn_lcc_letters', N'FN') IS NOT NULL
        DROP FUNCTION dbo.fn_lcc_letters;
    RETURN;
END

IF EXISTS (SELECT 1 FROM #plan WHERE conflict IS NOT NULL)
BEGIN
    RAISERROR(N'Identity conflicts exist. No changes were written. Review the preview.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.identity_rollback_20260924', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.identity_rollback_20260924 (
        user_id int NOT NULL PRIMARY KEY,
        email nvarchar(255) NULL,
        password_hash nvarchar(500) NULL,
        must_change_password bit NOT NULL,
        created_by_script bit NOT NULL
    );
END

INSERT INTO dbo.identity_rollback_20260924 (user_id, email, password_hash, must_change_password, created_by_script)
SELECT u.user_id, u.email, u.password_hash, u.must_change_password, 0
FROM dbo.users AS u
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.identity_rollback_20260924 AS b WHERE b.user_id = u.user_id);

UPDATE u
SET u.email = p.new_email
FROM dbo.users AS u
INNER JOIN #plan AS p ON p.user_id = u.user_id
WHERE p.account_kind = N'Student'
  AND p.new_email IS NOT NULL
  AND u.email <> p.new_email;

UPDATE u
SET u.email = p.new_email
FROM dbo.users AS u
INNER JOIN #plan AS p ON p.user_id = u.user_id
WHERE p.account_kind = N'Staff'
  AND p.new_email IS NOT NULL
  AND u.email <> p.new_email;

UPDATE u
SET
    u.password_hash = @PasswordHash,
    u.must_change_password = 1
FROM dbo.users AS u
WHERE u.status = N'Active';

DECLARE @office TABLE (
    email nvarchar(255) NOT NULL,
    role nvarchar(30) NOT NULL
);
INSERT INTO @office (email, role) VALUES
    (N'registration@lccbportal.org', N'Registrar/Admin'),
    (N'admissions@lccbportal.org', N'Registrar/Admin'),
    (N'principal@lccbportal.org', N'Management/Principal'),
    (N'finance@lccbportal.org', N'Registrar/Admin'),
    (N'noreply@lccbportal.org', N'Registrar/Admin');

INSERT INTO dbo.users (
    entra_id, email, password_hash, role, status, created_at, must_change_password, activation_used)
SELECT
    CONVERT(nvarchar(36), NEWID()),
    o.email,
    @PasswordHash,
    o.role,
    N'Active',
    SYSUTCDATETIME(),
    1,
    0
FROM @office AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.users AS u WHERE u.email = o.email);

INSERT INTO dbo.identity_rollback_20260924 (user_id, email, password_hash, must_change_password, created_by_script)
SELECT u.user_id, u.email, NULL, 1, 1
FROM dbo.users AS u
INNER JOIN @office AS o ON o.email = u.email
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.identity_rollback_20260924 AS b WHERE b.user_id = u.user_id);

COMMIT TRANSACTION;

PRINT N'Identity update committed. Active users must sign in with LccCms!2026 and choose a new password.';

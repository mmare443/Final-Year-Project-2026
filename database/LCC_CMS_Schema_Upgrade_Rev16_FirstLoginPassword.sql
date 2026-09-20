/* Rev 16 — first-login password change.
   users.must_change_password BIT NOT NULL DEFAULT 1.
   Existing portal accounts are flagged. No exempt system accounts. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.users', N'U') IS NULL
    THROW 50160, N'Rev16 abort: dbo.users is missing.', 1;
GO

IF COL_LENGTH(N'dbo.users', N'must_change_password') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD must_change_password BIT NOT NULL
        CONSTRAINT DF_users_must_change_password DEFAULT (1);
    PRINT N'Added dbo.users.must_change_password.';
END
ELSE
BEGIN
    PRINT N'Skipped users.must_change_password (already exists).';
END;
GO

UPDATE dbo.users
SET must_change_password = 1
WHERE must_change_password = 0;

SELECT COUNT(*) AS users_flagged
FROM dbo.users
WHERE must_change_password = 1;
GO

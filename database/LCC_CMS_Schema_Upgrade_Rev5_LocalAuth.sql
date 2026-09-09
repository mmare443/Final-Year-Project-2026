/* Local JWT login: users.password_hash (ASP.NET Identity v3 hash).
   Idempotent. Run against LCCCMSDB after Rev 4. */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.users', N'U') IS NULL
    THROW 50021, N'Rev5 abort: dbo.users is missing.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.users', N'password_hash') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD password_hash NVARCHAR(500) NULL;
    PRINT N'Added dbo.users.password_hash.';
END
ELSE
    PRINT N'Skipped users.password_hash (already exists).';

COMMIT TRANSACTION;
PRINT N'Rev 5 local-auth column committed.';
GO

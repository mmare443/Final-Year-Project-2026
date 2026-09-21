/* Rev 19 — forgot password reset tokens on dbo.users. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.users', N'U') IS NULL
    THROW 50190, N'Rev19 abort: dbo.users is missing.', 1;
GO

IF COL_LENGTH(N'dbo.users', N'password_reset_token') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD password_reset_token NVARCHAR(200) NULL;
    PRINT N'Added dbo.users.password_reset_token.';
END
ELSE
    PRINT N'Skipped dbo.users.password_reset_token (already exists).';
GO

IF COL_LENGTH(N'dbo.users', N'password_reset_expires_at') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD password_reset_expires_at DATETIME2 NULL;
    PRINT N'Added dbo.users.password_reset_expires_at.';
END
ELSE
    PRINT N'Skipped dbo.users.password_reset_expires_at (already exists).';
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_users_password_reset_token'
      AND object_id = OBJECT_ID(N'dbo.users')
)
BEGIN
    CREATE UNIQUE INDEX UX_users_password_reset_token
        ON dbo.users (password_reset_token)
        WHERE password_reset_token IS NOT NULL;
    PRINT N'Created UX_users_password_reset_token.';
END
ELSE
    PRINT N'Skipped UX_users_password_reset_token (already exists).';
GO

SELECT
    COL_LENGTH(N'dbo.users', N'password_reset_token') AS password_reset_token,
    COL_LENGTH(N'dbo.users', N'password_reset_expires_at') AS password_reset_expires_at;
GO

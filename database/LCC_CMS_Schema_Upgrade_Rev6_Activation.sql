/* Rev 6 — student activation tokens (one-time, 7-day expiry).
   Idempotent. Run against LCCCMSDB after Rev 5.

   Does not change users.status CHECK (Active/Inactive).
   Demo accounts keep NULL activation_token and existing password_hash. */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.users', N'U') IS NULL
    THROW 50061, N'Rev6 abort: dbo.users is missing.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.users', N'activation_token') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD activation_token NVARCHAR(128) NULL;
    PRINT N'Added dbo.users.activation_token.';
END
ELSE
    PRINT N'Skipped users.activation_token (already exists).';

IF COL_LENGTH(N'dbo.users', N'activation_expires_at') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD activation_expires_at DATETIME2 NULL;
    PRINT N'Added dbo.users.activation_expires_at.';
END
ELSE
    PRINT N'Skipped users.activation_expires_at (already exists).';

IF COL_LENGTH(N'dbo.users', N'activation_used') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD activation_used BIT NOT NULL
        CONSTRAINT DF_users_activation_used DEFAULT (0);
    PRINT N'Added dbo.users.activation_used.';
END
ELSE
    PRINT N'Skipped users.activation_used (already exists).';

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_users_activation_token'
      AND object_id = OBJECT_ID(N'dbo.users')
)
BEGIN
    CREATE UNIQUE INDEX UX_users_activation_token
        ON dbo.users(activation_token)
        WHERE activation_token IS NOT NULL;
    PRINT N'Created UX_users_activation_token.';
END
ELSE
    PRINT N'Skipped UX_users_activation_token (already exists).';

COMMIT TRANSACTION;
PRINT N'Rev 6 activation columns committed.';
GO

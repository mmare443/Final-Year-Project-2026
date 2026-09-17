/* Rev 6 — student activation tokens (one-time, 7-day expiry).
   Idempotent. GO-batched so newly added columns are visible to later statements. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.users', N'U') IS NULL
    THROW 50061, N'Rev6 abort: dbo.users is missing.', 1;
GO

IF COL_LENGTH(N'dbo.users', N'activation_token') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD activation_token NVARCHAR(128) NULL;
END;
GO

IF COL_LENGTH(N'dbo.users', N'activation_expires_at') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD activation_expires_at DATETIME2 NULL;
END;
GO

IF COL_LENGTH(N'dbo.users', N'activation_used') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD activation_used BIT NOT NULL
        CONSTRAINT DF_users_activation_used DEFAULT (0);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_users_activation_token'
      AND object_id = OBJECT_ID(N'dbo.users')
)
BEGIN
    CREATE UNIQUE INDEX UX_users_activation_token
        ON dbo.users(activation_token)
        WHERE activation_token IS NOT NULL;
END;
GO

SELECT
    COL_LENGTH(N'dbo.users', N'activation_token') AS activation_token,
    COL_LENGTH(N'dbo.users', N'activation_expires_at') AS activation_expires_at,
    COL_LENGTH(N'dbo.users', N'activation_used') AS activation_used;
GO

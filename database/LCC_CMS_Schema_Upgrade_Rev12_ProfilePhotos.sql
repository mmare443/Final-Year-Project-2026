/* Profile photos — users.profile_photo_url.
   Idempotent. Safe if Rev12 news (Open Graph) has already been applied. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.users', N'U') IS NULL
    THROW 50130, N'Profile photo abort: dbo.users is missing.', 1;
GO

IF COL_LENGTH(N'dbo.users', N'profile_photo_url') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD profile_photo_url NVARCHAR(500) NULL;
END;
GO

SELECT COL_LENGTH(N'dbo.users', N'profile_photo_url') AS profile_photo_url;
GO

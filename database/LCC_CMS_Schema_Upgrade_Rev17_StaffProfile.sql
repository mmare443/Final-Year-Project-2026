/* Rev 17 — staff personal information and emergency contacts.
   Staff self-service fields. Registrar still owns role, department,
   job title, and employment status. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.staff', N'U') IS NULL
    THROW 50170, N'Rev17 abort: dbo.staff is missing.', 1;
GO

IF COL_LENGTH(N'dbo.staff', N'phone_number') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD phone_number NVARCHAR(30) NULL;
    PRINT N'Added dbo.staff.phone_number.';
END
ELSE
    PRINT N'Skipped staff.phone_number (already exists).';
GO

IF COL_LENGTH(N'dbo.staff', N'personal_email') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD personal_email NVARCHAR(255) NULL;
    PRINT N'Added dbo.staff.personal_email.';
END
ELSE
    PRINT N'Skipped staff.personal_email (already exists).';
GO

IF COL_LENGTH(N'dbo.staff', N'postal_address') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD postal_address NVARCHAR(500) NULL;
    PRINT N'Added dbo.staff.postal_address.';
END
ELSE
    PRINT N'Skipped staff.postal_address (already exists).';
GO

IF COL_LENGTH(N'dbo.staff', N'province') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD province NVARCHAR(100) NULL;
    PRINT N'Added dbo.staff.province.';
END
ELSE
    PRINT N'Skipped staff.province (already exists).';
GO

IF COL_LENGTH(N'dbo.staff', N'district') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD district NVARCHAR(100) NULL;
    PRINT N'Added dbo.staff.district.';
END
ELSE
    PRINT N'Skipped staff.district (already exists).';
GO

IF COL_LENGTH(N'dbo.staff', N'village') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD village NVARCHAR(100) NULL;
    PRINT N'Added dbo.staff.village.';
END
ELSE
    PRINT N'Skipped staff.village (already exists).';
GO

IF COL_LENGTH(N'dbo.staff', N'emergency_contact_name') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD emergency_contact_name NVARCHAR(150) NULL;
    PRINT N'Added dbo.staff.emergency_contact_name.';
END
ELSE
    PRINT N'Skipped staff.emergency_contact_name (already exists).';
GO

IF COL_LENGTH(N'dbo.staff', N'emergency_contact_phone') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD emergency_contact_phone NVARCHAR(30) NULL;
    PRINT N'Added dbo.staff.emergency_contact_phone.';
END
ELSE
    PRINT N'Skipped staff.emergency_contact_phone (already exists).';
GO

IF COL_LENGTH(N'dbo.staff', N'emergency_relationship') IS NULL
BEGIN
    ALTER TABLE dbo.staff ADD emergency_relationship NVARCHAR(100) NULL;
    PRINT N'Added dbo.staff.emergency_relationship.';
END
ELSE
    PRINT N'Skipped staff.emergency_relationship (already exists).';
GO

SELECT
    COL_LENGTH(N'dbo.staff', N'phone_number') AS phone_number,
    COL_LENGTH(N'dbo.staff', N'personal_email') AS personal_email,
    COL_LENGTH(N'dbo.staff', N'postal_address') AS postal_address,
    COL_LENGTH(N'dbo.staff', N'province') AS province,
    COL_LENGTH(N'dbo.staff', N'district') AS district,
    COL_LENGTH(N'dbo.staff', N'village') AS village,
    COL_LENGTH(N'dbo.staff', N'emergency_contact_name') AS emergency_contact_name,
    COL_LENGTH(N'dbo.staff', N'emergency_contact_phone') AS emergency_contact_phone,
    COL_LENGTH(N'dbo.staff', N'emergency_relationship') AS emergency_relationship;
GO

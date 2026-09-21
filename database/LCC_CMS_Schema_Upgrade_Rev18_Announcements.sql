/* Rev 18 — operational announcements (not news).
   Dedicated dbo.announcements table. Notices remain a separate legacy table. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.staff', N'U') IS NULL
    THROW 50180, N'Rev18 abort: dbo.staff is missing.', 1;
GO

IF OBJECT_ID(N'dbo.announcements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.announcements (
        announcement_id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_announcements PRIMARY KEY,
        title           NVARCHAR(150)  NOT NULL,
        content         NVARCHAR(MAX)  NOT NULL,
        audience        NVARCHAR(20)   NOT NULL
            CONSTRAINT CK_announcements_audience
                CHECK (audience IN (N'PUBLIC', N'STUDENT', N'STAFF', N'EVERYONE')),
        start_date      DATE           NULL,
        end_date        DATE           NULL,
        priority        NVARCHAR(20)   NOT NULL
            CONSTRAINT DF_announcements_priority DEFAULT (N'Normal')
            CONSTRAINT CK_announcements_priority
                CHECK (priority IN (N'Normal', N'High', N'Urgent')),
        is_published    BIT            NOT NULL
            CONSTRAINT DF_announcements_is_published DEFAULT (0),
        is_archived     BIT            NOT NULL
            CONSTRAINT DF_announcements_is_archived DEFAULT (0),
        archived_at     DATETIME2      NULL,
        created_by      INT            NOT NULL
            CONSTRAINT FK_announcements_created_by REFERENCES dbo.staff(staff_id),
        created_at      DATETIME2      NOT NULL
            CONSTRAINT DF_announcements_created_at DEFAULT (SYSUTCDATETIME()),
        updated_at      DATETIME2      NULL
    );

    CREATE INDEX IX_announcements_feed
        ON dbo.announcements (is_published, is_archived, audience, start_date, end_date, priority);

    PRINT N'Created dbo.announcements.';
END
ELSE
    PRINT N'Skipped dbo.announcements (already exists).';
GO

SELECT
    OBJECT_ID(N'dbo.announcements', N'U') AS announcements_table,
    COL_LENGTH(N'dbo.announcements', N'is_published') AS is_published,
    COL_LENGTH(N'dbo.announcements', N'is_archived') AS is_archived,
    COL_LENGTH(N'dbo.announcements', N'audience') AS audience;
GO

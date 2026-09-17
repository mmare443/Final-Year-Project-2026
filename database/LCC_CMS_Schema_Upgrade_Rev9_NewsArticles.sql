/* Rev 9 — public website news articles.
   Idempotent. GO-batched so newly added objects are visible to later statements. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.users', N'U') IS NULL
    THROW 50091, N'Rev9 abort: dbo.users is missing.', 1;
GO

IF OBJECT_ID(N'dbo.news_articles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.news_articles
    (
        news_id        INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_news_articles PRIMARY KEY,
        title          NVARCHAR(200) NOT NULL,
        summary        NVARCHAR(500) NOT NULL,
        content        NVARCHAR(MAX) NOT NULL,
        image_url      NVARCHAR(500) NULL,
        published_at   DATETIME2 NULL,
        is_published   BIT NOT NULL
            CONSTRAINT DF_news_articles_is_published DEFAULT (0),
        created_by     INT NOT NULL
            CONSTRAINT FK_news_articles_created_by REFERENCES dbo.users(user_id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_news_articles_published'
      AND object_id = OBJECT_ID(N'dbo.news_articles')
)
BEGIN
    CREATE INDEX IX_news_articles_published
        ON dbo.news_articles(is_published, published_at DESC);
END;
GO

DECLARE @createdBy INT = (
    SELECT TOP (1) user_id
    FROM dbo.users
    WHERE email = N'registrar@lccb.ac.pg' AND status = N'Active'
);

IF @createdBy IS NULL
BEGIN
    SET @createdBy = (
        SELECT TOP (1) user_id
        FROM dbo.users
        WHERE role IN (N'Registrar/Admin', N'Management/Principal')
          AND status = N'Active'
        ORDER BY user_id
    );
END;

IF @createdBy IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.news_articles)
BEGIN
    INSERT INTO dbo.news_articles
        (title, summary, content, image_url, published_at, is_published, created_by)
    VALUES
        (N'Semester 1 studies underway',
         N'Students have returned to campus for lectures, chapel and college life in Banz.',
         N'Semester 1 is underway at Lutheran Church College. Students are attending lectures, chapel and college activities on campus in Banz.

Heads of Department remind students to complete course registration and to keep attendance above the college requirement.

Further notices will be posted as the semester continues.',
         N'images/college/lccpr.jpg',
         SYSUTCDATETIME(),
         1,
         @createdBy),
        (N'Admissions remain open',
         N'Applications for diploma programmes are being received at the College office.',
         N'Lutheran Church College continues to receive applications for diploma programmes. Prospective students should submit a completed application with supporting documents to the College office.

Contact details are listed on the College website. Successful applicants will receive an activation email to set a portal password.',
         N'images/college/lccam.jpg',
         SYSUTCDATETIME(),
         1,
         @createdBy);
END;
GO

SELECT COUNT(*) AS news_articles FROM dbo.news_articles;
GO

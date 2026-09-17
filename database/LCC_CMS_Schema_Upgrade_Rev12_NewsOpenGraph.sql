/* Rev 12 — Open Graph thumbnail and source title for external news.
   Preserves existing news_articles rows. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.news_articles', N'U') IS NULL
    THROW 50121, N'Rev12 abort: dbo.news_articles is missing. Apply Rev9 first.', 1;
GO

IF COL_LENGTH(N'dbo.news_articles', N'thumbnail_url') IS NULL
BEGIN
    ALTER TABLE dbo.news_articles ADD thumbnail_url NVARCHAR(1000) NULL;
END;
GO

IF COL_LENGTH(N'dbo.news_articles', N'source_title') IS NULL
BEGIN
    ALTER TABLE dbo.news_articles ADD source_title NVARCHAR(500) NULL;
END;
GO

UPDATE dbo.news_articles
SET source_title = title
WHERE is_external = 1
  AND source_title IS NULL
  AND title IS NOT NULL;
GO

SELECT
    COL_LENGTH(N'dbo.news_articles', N'thumbnail_url') AS thumbnail_url,
    COL_LENGTH(N'dbo.news_articles', N'source_title') AS source_title;

SELECT COUNT(*) AS news_articles FROM dbo.news_articles;
GO

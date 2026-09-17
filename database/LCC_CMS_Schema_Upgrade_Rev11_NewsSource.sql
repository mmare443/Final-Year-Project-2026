/* Rev 11 — source metadata for external news stories.
   Adds source_subtitle and source_logo_url. Preserves Rev9/Rev10 rows. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.news_articles', N'U') IS NULL
    THROW 50111, N'Rev11 abort: dbo.news_articles is missing. Apply Rev9 first.', 1;
GO

IF COL_LENGTH(N'dbo.news_articles', N'source_subtitle') IS NULL
BEGIN
    ALTER TABLE dbo.news_articles ADD source_subtitle NVARCHAR(300) NULL;
END;
GO

IF COL_LENGTH(N'dbo.news_articles', N'source_logo_url') IS NULL
BEGIN
    ALTER TABLE dbo.news_articles ADD source_logo_url NVARCHAR(500) NULL;
END;
GO

UPDATE dbo.news_articles
SET source_subtitle = source_name
WHERE is_external = 1
  AND source_subtitle IS NULL
  AND source_name IS NOT NULL
  AND source_name NOT IN (N'Facebook', N'YouTube', N'Post Courier', N'The National', N'EMTV', N'NBC');
GO

UPDATE dbo.news_articles
SET
    source_name = N'Facebook',
    source_logo_url = N'images/college/news/logos/facebook.svg'
WHERE is_external = 1
  AND external_url LIKE N'%facebook.com%';

UPDATE dbo.news_articles
SET
    source_name = N'YouTube',
    source_logo_url = N'images/college/news/logos/youtube.svg'
WHERE is_external = 1
  AND (external_url LIKE N'%youtube.com%' OR external_url LIKE N'%youtu.be%');

UPDATE dbo.news_articles
SET
    source_name = N'Post Courier',
    source_logo_url = N'images/college/news/logos/postcourier.svg'
WHERE is_external = 1
  AND external_url LIKE N'%postcourier.com.pg%';

UPDATE dbo.news_articles
SET
    source_name = N'The National',
    source_logo_url = N'images/college/news/logos/thenational.svg'
WHERE is_external = 1
  AND external_url LIKE N'%thenational.com.pg%';

UPDATE dbo.news_articles
SET
    source_name = N'EMTV',
    source_logo_url = N'images/college/news/logos/emtv.svg'
WHERE is_external = 1
  AND external_url LIKE N'%emtv.com.pg%';

UPDATE dbo.news_articles
SET
    source_name = N'NBC',
    source_logo_url = N'images/college/news/logos/nbc.svg'
WHERE is_external = 1
  AND (external_url LIKE N'%nbc.com.pg%' OR external_url LIKE N'%nbcpng%');
GO

SELECT
    COL_LENGTH(N'dbo.news_articles', N'source_name') AS source_name,
    COL_LENGTH(N'dbo.news_articles', N'source_subtitle') AS source_subtitle,
    COL_LENGTH(N'dbo.news_articles', N'source_logo_url') AS source_logo_url;

SELECT COUNT(*) AS news_articles FROM dbo.news_articles;
GO

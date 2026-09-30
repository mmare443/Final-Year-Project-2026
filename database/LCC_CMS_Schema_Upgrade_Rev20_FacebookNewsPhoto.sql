/* Rev 20 — Facebook news no longer stores page Open Graph images or the college motto.
   The public card uses the default placeholder until a post photo is uploaded.
   No new columns. Safe to run more than once. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.news_articles', N'U') IS NULL
    THROW 50200, N'Rev20 abort: dbo.news_articles is missing.', 1;
GO

UPDATE dbo.news_articles
SET thumbnail_url = NULL
WHERE is_external = 1
  AND (
        external_url LIKE N'%facebook.com%'
        OR external_url LIKE N'%fb.com%'
        OR external_url LIKE N'%fb.watch%'
      )
  AND thumbnail_url IS NOT NULL
  AND thumbnail_url NOT LIKE N'news-file/%'
  AND (
        thumbnail_url LIKE N'%facebook.com%'
        OR thumbnail_url LIKE N'%fb.com%'
        OR thumbnail_url LIKE N'%fb.watch%'
        OR thumbnail_url LIKE N'%fbsbx.com%'
        OR thumbnail_url LIKE N'%fbcdn.net%'
      );

UPDATE dbo.news_articles
SET source_subtitle = NULL
WHERE news_id = 8
  AND source_subtitle LIKE N'%By Faith in God my potential manifests here%';

UPDATE dbo.news_articles
SET summary = N'Facebook'
WHERE news_id = 8
  AND summary LIKE N'%By Faith in God my potential manifests here%';
GO

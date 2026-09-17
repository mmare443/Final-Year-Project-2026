/* Rev 10 — external news links on news_articles.
   Idempotent. GO-batched so newly added columns are visible later. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.news_articles', N'U') IS NULL
    THROW 50101, N'Rev10 abort: dbo.news_articles is missing. Apply Rev9 first.', 1;
GO

IF COL_LENGTH(N'dbo.news_articles', N'external_url') IS NULL
BEGIN
    ALTER TABLE dbo.news_articles ADD external_url NVARCHAR(1000) NULL;
END;
GO

IF COL_LENGTH(N'dbo.news_articles', N'source_name') IS NULL
BEGIN
    ALTER TABLE dbo.news_articles ADD source_name NVARCHAR(200) NULL;
END;
GO

IF COL_LENGTH(N'dbo.news_articles', N'is_external') IS NULL
BEGIN
    ALTER TABLE dbo.news_articles ADD is_external BIT NOT NULL
        CONSTRAINT DF_news_articles_is_external DEFAULT (0);
END;
GO

SELECT
    COL_LENGTH(N'dbo.news_articles', N'external_url') AS external_url,
    COL_LENGTH(N'dbo.news_articles', N'source_name') AS source_name,
    COL_LENGTH(N'dbo.news_articles', N'is_external') AS is_external;
GO

using Microsoft.EntityFrameworkCore;
using LCC_CMS_Api.Models;

namespace LCC_CMS_Api.Services;

public static class NewsMetadataBackfill
{
    public static async Task RunAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LccCmsDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<OpenGraphMetadataClient>();

        List<NewsArticle> rows;
        try
        {
            rows = await db.NewsArticles
                .Where(a => a.IsExternal
                    && a.ExternalUrl != null
                    && (a.ThumbnailUrl == null
                        || a.SourceLogoUrl == null
                        || a.SourceTitle == null
                        || a.SourceSubtitle == null
                        || a.ThumbnailUrl.Contains("facebook.com/")
                        || a.ThumbnailUrl.Contains("fbsbx.com")
                        || a.ThumbnailUrl.Contains("fbcdn.net")
                        || a.ExternalUrl.Contains("facebook.com")
                        || a.ExternalUrl.Contains("fb.com")))
                .Take(25)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "News Open Graph backfill skipped: {Message}", ex.GetBaseException().Message);
            return;
        }

        var changed = 0;
        foreach (var row in rows)
        {
            if (FacebookLinkMetadata.IsStoredFile(row.ThumbnailUrl))
            {
                continue;
            }

            if (FacebookLinkMetadata.IsFacebook(row.ExternalUrl))
            {
                var facebookChanged = false;
                if (FacebookLinkMetadata.IsRemoteFacebookImage(row.ThumbnailUrl))
                {
                    row.ThumbnailUrl = null;
                    facebookChanged = true;
                }

                if (IsCollegeMotto(row.SourceSubtitle))
                {
                    row.SourceSubtitle = null;
                    facebookChanged = true;
                }

                if (IsCollegeMotto(row.Summary))
                {
                    row.Summary = row.SourceName ?? "";
                    facebookChanged = true;
                }

                if (facebookChanged) changed++;
                continue;
            }

            var detected = NewsSourceCatalog.Detect(row.ExternalUrl);
            var updated = false;

            if (detected is not null)
            {
                if (string.IsNullOrWhiteSpace(row.SourceName))
                {
                    row.SourceName = detected.Name;
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(row.SourceLogoUrl))
                {
                    row.SourceLogoUrl = detected.LogoUrl;
                    updated = true;
                }
            }

            var meta = await client.FetchAsync(row.ExternalUrl, CancellationToken.None);
            if (meta is null)
            {
                if (updated) changed++;
                continue;
            }

            var metadataUpdated = false;
            if (string.IsNullOrWhiteSpace(row.SourceTitle) && !string.IsNullOrWhiteSpace(meta.Title))
            {
                row.SourceTitle = Trim(meta.Title, 500);
                metadataUpdated = true;
            }

            if (string.IsNullOrWhiteSpace(row.SourceSubtitle) && !string.IsNullOrWhiteSpace(meta.Description))
            {
                row.SourceSubtitle = Trim(meta.Description, 300);
                metadataUpdated = true;
            }

            if (string.IsNullOrWhiteSpace(row.ThumbnailUrl)
                && !string.IsNullOrWhiteSpace(meta.ImageUrl)
                && !FacebookLinkMetadata.IsRemoteFacebookImage(meta.ImageUrl))
            {
                row.ThumbnailUrl = Trim(meta.ImageUrl, 1000);
                metadataUpdated = true;
            }

            if (updated || metadataUpdated) changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Backfilled Open Graph metadata for {Count} external news stories.", changed);
        }
    }

    private static bool IsCollegeMotto(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains("By Faith in God my potential manifests here", StringComparison.OrdinalIgnoreCase);
    }

    private static string Trim(string value, int max)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}

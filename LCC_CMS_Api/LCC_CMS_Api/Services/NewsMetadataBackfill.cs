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
                    && (a.ThumbnailUrl == null || a.SourceLogoUrl == null || a.SourceTitle == null))
                .Take(25)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not backfill news Open Graph metadata. Apply Rev12.");
            return;
        }

        var changed = 0;
        foreach (var row in rows)
        {
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

            var updated = false;
            if (string.IsNullOrWhiteSpace(row.SourceTitle) && !string.IsNullOrWhiteSpace(meta.Title))
            {
                row.SourceTitle = Trim(meta.Title, 500);
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(row.SourceSubtitle) && !string.IsNullOrWhiteSpace(meta.Description))
            {
                row.SourceSubtitle = Trim(meta.Description, 300);
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(row.ThumbnailUrl) && !string.IsNullOrWhiteSpace(meta.ImageUrl))
            {
                row.ThumbnailUrl = Trim(meta.ImageUrl, 1000);
                updated = true;
            }

            if (updated) changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Backfilled Open Graph metadata for {Count} external news stories.", changed);
        }
    }

    private static string Trim(string value, int max)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}

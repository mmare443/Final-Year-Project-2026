using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// Public website news. Anonymous callers receive published articles only.
/// Principal/Admin (Management/Principal) can create, edit, publish, unpublish, and delete.
/// </summary>
[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly OpenGraphMetadataClient _openGraph;

    public NewsController(
        LccCmsDbContext dbContext,
        ICurrentUser currentUser,
        OpenGraphMetadataClient openGraph)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _openGraph = openGraph;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NewsArticleRecord>>> GetNews(CancellationToken cancellationToken)
    {
        var query = ArticleGraph().AsNoTracking();
        if (!await CanManageAsync(cancellationToken))
        {
            query = query.Where(a => a.IsPublished);
        }

        var articles = await query
            .OrderByDescending(a => a.PublishedAt)
            .ThenByDescending(a => a.NewsId)
            .ToListAsync(cancellationToken);

        return Ok(articles.Select(ToRecord));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpGet("preview")]
    public async Task<ActionResult<NewsPreviewRecord>> Preview(
        [FromQuery] string url,
        CancellationToken cancellationToken)
    {
        if (!OpenGraphMetadataClient.IsAllowedPublicHttpUrl(url, out _))
        {
            return BadRequest("Enter a public http(s) source URL.");
        }

        var detected = NewsSourceCatalog.Detect(url);
        var meta = await _openGraph.FetchAsync(url, cancellationToken);
        return Ok(new NewsPreviewRecord
        {
            ExternalUrl = url.Trim(),
            SourceName = detected?.Name,
            SourceLogoUrl = detected?.LogoUrl,
            SourceTitle = meta?.Title,
            SourceSubtitle = meta?.Description,
            ThumbnailUrl = meta?.ImageUrl,
        });
    }

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<NewsArticleRecord>> GetNewsArticle(int id, CancellationToken cancellationToken)
    {
        var article = await ArticleGraph()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.NewsId == id, cancellationToken);
        if (article is null) return NotFound();
        if (!article.IsPublished && !await CanManageAsync(cancellationToken))
        {
            return NotFound();
        }

        return Ok(ToRecord(article));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPost]
    public async Task<ActionResult<NewsArticleRecord>> CreateNews(
        [FromBody] NewsWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateWrite(request, requireTitle: !request.IsExternal);
        if (error is not null) return BadRequest(error);

        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is not int userId)
        {
            return Unauthorized();
        }

        await EnrichExternalAsync(request, cancellationToken);
        error = ValidateWrite(request, requireTitle: true);
        if (error is not null) return BadRequest(error);

        var article = new NewsArticle { CreatedBy = userId };
        ApplyWrite(article, request, publishingNow: request.IsPublished);

        _dbContext.NewsArticles.Add(article);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        article = await ArticleGraph().FirstAsync(a => a.NewsId == article.NewsId, cancellationToken);
        return Ok(ToRecord(article));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<NewsArticleRecord>> UpdateNews(
        int id,
        [FromBody] NewsWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateWrite(request, requireTitle: !request.IsExternal);
        if (error is not null) return BadRequest(error);

        var article = await ArticleGraph().FirstOrDefaultAsync(a => a.NewsId == id, cancellationToken);
        if (article is null) return NotFound();

        await EnrichExternalAsync(request, cancellationToken);
        error = ValidateWrite(request, requireTitle: true);
        if (error is not null) return BadRequest(error);

        var publishingNow = request.IsPublished && !article.IsPublished;
        ApplyWrite(article, request, publishingNow);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(ToRecord(article));
    }

    [Authorize(Policy = "PrincipalAdminOnly")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNews(int id, CancellationToken cancellationToken)
    {
        var article = await ArticleGraph().FirstOrDefaultAsync(a => a.NewsId == id, cancellationToken);
        if (article is null) return NotFound();

        var record = ToRecord(article);
        _dbContext.NewsArticles.Remove(article);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var message))
        {
            return StatusCode(status, message);
        }

        return Ok(record);
    }

    private async Task<bool> CanManageAsync(CancellationToken cancellationToken)
    {
        if (!User.Identity?.IsAuthenticated ?? true) return false;
        if (User.IsInRole(RoleNames.ManagementPrincipal) || User.IsInRole(RoleNames.ManagementPrincipalSql))
        {
            return true;
        }

        if (!await _currentUser.ResolveAsync(cancellationToken)) return false;
        var role = RoleNames.ToPolicyRole(_currentUser.Role);
        return role.Equals(RoleNames.ManagementPrincipal, StringComparison.OrdinalIgnoreCase);
    }

    private IQueryable<NewsArticle> ArticleGraph()
    {
        return _dbContext.NewsArticles.Include(a => a.CreatedByUser);
    }

    private static void ApplyWrite(NewsArticle article, NewsWriteRequest request, bool publishingNow)
    {
        article.Title = Clip(FirstNonEmpty(request.Title, request.SourceTitle), 200) ?? "";
        article.IsExternal = request.IsExternal;
        article.IsPublished = request.IsPublished;

        if (request.IsExternal)
        {
            var detected = NewsSourceCatalog.Detect(request.ExternalUrl);
            var sourceName = FirstNonEmpty(request.SourceName, detected?.Name);
            var logoUrl = FirstNonEmpty(request.SourceLogoUrl, detected?.LogoUrl);
            var subtitle = FirstNonEmpty(request.SourceSubtitle, request.Summary);

            article.Content = "";
            article.ImageUrl = null;
            article.ExternalUrl = (request.ExternalUrl ?? "").Trim();
            article.SourceName = sourceName;
            article.SourceSubtitle = subtitle;
            article.SourceLogoUrl = logoUrl;
            article.SourceTitle = Clip(FirstNonEmpty(request.SourceTitle, request.Title), 500);
            article.ThumbnailUrl = Clip(FirstNonEmpty(request.ThumbnailUrl), 1000);
            article.Summary = FirstNonEmpty(request.Summary, subtitle, sourceName) ?? "";
            if (string.IsNullOrWhiteSpace(article.Title) && !string.IsNullOrWhiteSpace(article.SourceTitle))
            {
                article.Title = Clip(article.SourceTitle, 200) ?? article.Title;
            }
        }
        else
        {
            article.Summary = request.Summary.Trim();
            article.Content = request.Content.Trim();
            article.ImageUrl = NormalizeOptional(request.ImageUrl);
            article.ExternalUrl = null;
            article.SourceName = null;
            article.SourceSubtitle = null;
            article.SourceLogoUrl = null;
            article.SourceTitle = null;
            article.ThumbnailUrl = null;
        }

        if (publishingNow)
        {
            article.PublishedAt ??= DateTime.UtcNow;
        }
    }

    private async Task EnrichExternalAsync(NewsWriteRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsExternal) return;

        var meta = await _openGraph.FetchAsync(request.ExternalUrl, cancellationToken);
        if (meta is null) return;

        request.SourceTitle = FirstNonEmpty(request.SourceTitle, meta.Title);
        request.SourceSubtitle = FirstNonEmpty(request.SourceSubtitle, request.Summary, meta.Description);
        request.ThumbnailUrl = FirstNonEmpty(request.ThumbnailUrl, meta.ImageUrl);
        request.Title = FirstNonEmpty(request.Title, meta.Title) ?? request.Title;
        request.Summary = FirstNonEmpty(request.Summary, meta.Description) ?? request.Summary ?? "";
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string? ValidateWrite(NewsWriteRequest request, bool requireTitle)
    {
        if (requireTitle)
        {
            if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.SourceTitle))
            {
                return "Title is required.";
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Title) && request.Title.Trim().Length > 200)
        {
            return "Title must be 200 characters or fewer.";
        }

        if (request.IsExternal)
        {
            if (string.IsNullOrWhiteSpace(request.ExternalUrl)) return "Source URL is required for an external news story.";
            if (request.ExternalUrl.Trim().Length > 1000) return "Source URL must be 1000 characters or fewer.";
            if (!IsHttpUrl(request.ExternalUrl)) return "Source URL must start with http:// or https://.";

            var detected = NewsSourceCatalog.Detect(request.ExternalUrl);
            if (string.IsNullOrWhiteSpace(request.SourceName) && detected is null)
            {
                return "Source name is required when the URL is not a recognised outlet.";
            }

            if (!string.IsNullOrWhiteSpace(request.SourceName) && request.SourceName.Trim().Length > 200)
            {
                return "Source name must be 200 characters or fewer.";
            }

            if (!string.IsNullOrWhiteSpace(request.SourceSubtitle) && request.SourceSubtitle.Trim().Length > 300)
            {
                return "Source subtitle must be 300 characters or fewer.";
            }

            if (!IsAllowedImageUrl(request.SourceLogoUrl, out var logoError)) return logoError;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Summary)) return "Summary is required.";
            if (request.Summary.Trim().Length > 500) return "Summary must be 500 characters or fewer.";
            if (string.IsNullOrWhiteSpace(request.Content)) return "Content is required for an internal article.";
            if (!IsAllowedImageUrl(request.ImageUrl, out var imageError)) return imageError;
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return null;
    }

    private static bool IsHttpUrl(string? value)
    {
        var url = value?.Trim() ?? "";
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedImageUrl(string? value, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value)) return true;

        var url = value.Trim();
        if (url.Length > 500)
        {
            error = "Image URL must be 500 characters or fewer.";
            return false;
        }

        if (url.Contains("://", StringComparison.Ordinal)
            && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            error = "Image URL must be http(s) or a site-relative path.";
            return false;
        }

        return true;
    }

    private static string? NormalizeOptional(string? value)
    {
        var url = value?.Trim();
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    private static NewsArticleRecord ToRecord(NewsArticle article)
    {
        return new NewsArticleRecord
        {
            NewsId = article.NewsId,
            Id = article.NewsId,
            Title = article.Title,
            Summary = article.Summary,
            Content = article.Content,
            ImageUrl = article.ImageUrl,
            ExternalUrl = article.ExternalUrl,
            SourceName = article.SourceName,
            SourceSubtitle = article.SourceSubtitle,
            SourceLogoUrl = article.SourceLogoUrl,
            SourceTitle = article.SourceTitle,
            ThumbnailUrl = article.ThumbnailUrl,
            IsExternal = article.IsExternal,
            PublishedAt = article.PublishedAt,
            IsPublished = article.IsPublished,
            CreatedBy = article.CreatedBy,
            CreatedByEmail = article.CreatedByUser?.Email ?? "",
        };
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the news article.";

        if (ex.InnerException is not SqlException sql)
        {
            return false;
        }

        if (sql.Number == 208)
        {
            status = StatusCodes.Status503ServiceUnavailable;
            message = "The news_articles table is missing. Apply database/LCC_CMS_Schema_Upgrade_Rev9_NewsArticles.sql.";
            return true;
        }

        if (sql.Number is 207)
        {
            status = StatusCodes.Status503ServiceUnavailable;
            message = "News columns are missing. Apply database/LCC_CMS_Schema_Upgrade_Rev12_NewsOpenGraph.sql.";
            return true;
        }

        if (sql.Number == 547)
        {
            message = "A related record was not found (created_by).";
            return true;
        }

        return false;
    }
}

public class NewsArticleRecord
{
    public int NewsId { get; set; }
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public string? SourceName { get; set; }
    public string? SourceSubtitle { get; set; }
    public string? SourceLogoUrl { get; set; }
    public string? SourceTitle { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsExternal { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsPublished { get; set; }
    public int CreatedBy { get; set; }
    public string CreatedByEmail { get; set; } = "";
}

public class NewsWriteRequest
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public string? SourceName { get; set; }
    public string? SourceSubtitle { get; set; }
    public string? SourceLogoUrl { get; set; }
    public string? SourceTitle { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsExternal { get; set; }
    public bool IsPublished { get; set; }
}

public class NewsPreviewRecord
{
    public string ExternalUrl { get; set; } = "";
    public string? SourceName { get; set; }
    public string? SourceLogoUrl { get; set; }
    public string? SourceTitle { get; set; }
    public string? SourceSubtitle { get; set; }
    public string? ThumbnailUrl { get; set; }
}

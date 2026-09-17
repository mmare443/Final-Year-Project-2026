namespace LCC_CMS_Api.Models;

public partial class NewsArticle
{
    public int NewsId { get; set; }

    public string Title { get; set; } = null!;

    public string Summary { get; set; } = null!;

    public string Content { get; set; } = null!;

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

    public virtual User CreatedByUser { get; set; } = null!;
}

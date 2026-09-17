namespace LCC_CMS_Api.Services;

public sealed record NewsSourceMatch(string Name, string LogoUrl);

public static class NewsSourceCatalog
{
    public const string LogoRoot = "images/college/news/logos/";

    public static NewsSourceMatch? Detect(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var host = TryHost(url);
        if (string.IsNullOrWhiteSpace(host)) return null;

        if (host.Contains("facebook.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fb.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fb.watch", StringComparison.OrdinalIgnoreCase))
        {
            return new NewsSourceMatch("Facebook", LogoRoot + "facebook.svg");
        }

        if (host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return new NewsSourceMatch("YouTube", LogoRoot + "youtube.svg");
        }

        if (host.Contains("postcourier.com.pg", StringComparison.OrdinalIgnoreCase))
        {
            return new NewsSourceMatch("Post Courier", LogoRoot + "postcourier.svg");
        }

        if (host.Contains("thenational.com.pg", StringComparison.OrdinalIgnoreCase))
        {
            return new NewsSourceMatch("The National", LogoRoot + "thenational.svg");
        }

        if (host.Contains("emtv.com.pg", StringComparison.OrdinalIgnoreCase))
        {
            return new NewsSourceMatch("EMTV", LogoRoot + "emtv.svg");
        }

        if (host.Contains("nbc.com.pg", StringComparison.OrdinalIgnoreCase)
            || host.Contains("nbcpng", StringComparison.OrdinalIgnoreCase))
        {
            return new NewsSourceMatch("NBC", LogoRoot + "nbc.svg");
        }

        return null;
    }

    private static string TryHost(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return url;
        }

        return uri.Host;
    }
}

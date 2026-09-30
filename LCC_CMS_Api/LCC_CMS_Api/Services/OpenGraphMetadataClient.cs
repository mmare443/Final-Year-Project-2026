using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LCC_CMS_Api.Services;

public sealed record OpenGraphMetadata(
    string? Title,
    string? Description,
    string? ImageUrl);

public sealed record RemoteImage(byte[] Bytes, string ContentType);

public sealed class OpenGraphMetadataClient
{
    private const int MaxBytes = 512_000;
    private const int MaxImageBytes = 2_000_000;
    private readonly HttpClient _http;
    private readonly ILogger<OpenGraphMetadataClient> _logger;

    public OpenGraphMetadataClient(HttpClient http, ILogger<OpenGraphMetadataClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<OpenGraphMetadata?> FetchAsync(string? url, CancellationToken cancellationToken)
    {
        if (!IsAllowedPublicHttpUrl(url, out var uri))
        {
            return null;
        }

        try
        {
            if (IsYouTube(uri))
            {
                var oembed = await FetchYouTubeOEmbedAsync(uri, cancellationToken);
                if (oembed is not null) return oembed;
            }

            return await FetchHtmlOpenGraphAsync(uri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Open Graph fetch skipped for {Host}", uri.Host);
            return null;
        }
    }

    private async Task<OpenGraphMetadata?> FetchYouTubeOEmbedAsync(Uri page, CancellationToken cancellationToken)
    {
        var oembed = new Uri(
            "https://www.youtube.com/oembed?format=json&url=" + Uri.EscapeDataString(page.ToString()));
        using var youtubeRequest = new HttpRequestMessage(HttpMethod.Get, oembed);
        using var response = await SendFollowingAsync(youtubeRequest, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;
        var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
        var image = root.TryGetProperty("thumbnail_url", out var i) ? i.GetString() : null;
        var author = root.TryGetProperty("author_name", out var a) ? a.GetString() : null;
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(image)) return null;
        return new OpenGraphMetadata(title, author, image);
    }

    private async Task<OpenGraphMetadata?> FetchHtmlOpenGraphAsync(Uri page, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, page);
        request.Headers.TryAddWithoutValidation(
            "Accept", "text/html,application/xhtml+xml");
        if (page.Host.Contains("facebook.com", StringComparison.OrdinalIgnoreCase)
            || page.Host.Contains("fb.com", StringComparison.OrdinalIgnoreCase)
            || page.Host.Contains("fb.watch", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Remove("User-Agent");
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)");
        }
        using var response = await SendFollowingAsync(request, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "Open Graph fetch returned {Status} for {Host}",
                response is null ? 0 : (int)response.StatusCode,
                page.Host);
            return null;
        }

        var html = await ReadLimitedStringAsync(response, cancellationToken);
        if (string.IsNullOrWhiteSpace(html)) return null;

        var title = Meta(html, "og:title") ?? Meta(html, "twitter:title");
        var description = Meta(html, "og:description") ?? Meta(html, "twitter:description");
        var image = Meta(html, "og:image") ?? Meta(html, "twitter:image");
        image = ResolveMaybeRelative(page, image);

        if (string.IsNullOrWhiteSpace(title)
            && string.IsNullOrWhiteSpace(description)
            && string.IsNullOrWhiteSpace(image))
        {
            return null;
        }

        return new OpenGraphMetadata(
            Decode(title),
            Decode(description),
            image);
    }

    private static async Task<string> ReadLimitedStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var limited = new LimitedReadStream(stream, MaxBytes);
        using var reader = new StreamReader(limited);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string? Meta(string html, string property)
    {
        var pattern = $@"<meta\s[^>]*?(?:property|name)\s*=\s*[""']{Regex.Escape(property)}[""'][^>]*?content\s*=\s*[""']([^""']+)[""'][^>]*?>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        pattern = $@"<meta\s[^>]*?content\s*=\s*[""']([^""']+)[""'][^>]*?(?:property|name)\s*=\s*[""']{Regex.Escape(property)}[""'][^>]*?>";
        match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return WebUtility.HtmlDecode(value.Trim());
    }

    private static string? ResolveMaybeRelative(Uri page, string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return null;
        if (Uri.TryCreate(image, UriKind.Absolute, out var absolute)) return absolute.ToString();
        if (Uri.TryCreate(page, image, out var resolved)) return resolved.ToString();
        return image;
    }

    public Task<RemoteImage?> FetchImageAsync(string? url, CancellationToken cancellationToken)
    {
        return FetchImageAsync(url, cancellationToken, allowPageResolve: true);
    }

    private async Task<RemoteImage?> FetchImageAsync(
        string? url,
        CancellationToken cancellationToken,
        bool allowPageResolve)
    {
        if (!IsAllowedPublicHttpUrl(url, out var uri)) return null;

        if (IsFacebookPage(uri))
        {
            if (!allowPageResolve) return null;
            var meta = await FetchHtmlOpenGraphAsync(uri, cancellationToken);
            if (string.IsNullOrWhiteSpace(meta?.ImageUrl)) return null;
            return await FetchImageAsync(meta.ImageUrl, cancellationToken, allowPageResolve: false);
        }

        if (!IsEmbeddableImageHost(uri)) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/*,*/*");
            if (IsFacebookHost(uri))
            {
                request.Headers.TryAddWithoutValidation(
                    "User-Agent",
                    "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)");
            }

            using var response = await SendFollowingAsync(request, cancellationToken);
            if (response is null || !response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "News image fetch returned {Status} for {Host}",
                    response is null ? 0 : (int)response.StatusCode,
                    uri.Host);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var limited = new LimitedReadStream(stream, MaxImageBytes);
            using var buffer = new MemoryStream();
            await limited.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            if (bytes.Length == 0) return null;
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                contentType = SniffImageType(bytes) ?? "";
            }

            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new RemoteImage(bytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "News image fetch skipped for {Host}", uri.Host);
            return null;
        }
    }

    public static bool IsEmbeddableImageHost(string? url)
    {
        return IsAllowedPublicHttpUrl(url, out var uri) && IsEmbeddableImageHost(uri);
    }

    private async Task<HttpResponseMessage?> SendFollowingAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var current = request;
        for (var hop = 0; hop < 5; hop++)
        {
            var response = await _http.SendAsync(
                current,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            var status = (int)response.StatusCode;
            if (status is not (301 or 302 or 303 or 307 or 308))
            {
                return response;
            }

            var next = response.Headers.Location;
            response.Dispose();
            if (next is null) return null;
            if (!next.IsAbsoluteUri)
            {
                next = new Uri(current.RequestUri!, next);
            }

            if (!IsAllowedPublicHttpUrl(next.ToString(), out var safe)) return null;
            var follow = new HttpRequestMessage(HttpMethod.Get, safe);
            foreach (var header in current.Headers)
            {
                follow.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (!ReferenceEquals(current, request)) current.Dispose();
            current = follow;
        }

        current.Dispose();
        return null;
    }

    private static bool IsEmbeddableImageHost(Uri uri)
    {
        var host = uri.Host;
        return host.Equals("lookaside.fbsbx.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".fbcdn.net", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".fbsbx.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("i.ytimg.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".ytimg.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFacebookPage(Uri uri)
    {
        return IsFacebookHost(uri) && !IsEmbeddableImageHost(uri);
    }

    private static bool IsFacebookHost(Uri uri)
    {
        var host = uri.Host;
        return host.Contains("facebook.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fb.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fb.watch", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fbsbx.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".fbcdn.net", StringComparison.OrdinalIgnoreCase);
    }

    private static string? SniffImageType(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "image/png";
        if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return "image/gif";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46) return "image/webp";
        return null;
    }

    private static bool IsYouTube(Uri uri)
    {
        var host = uri.Host;
        return host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAllowedPublicHttpUrl(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri!)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (string.IsNullOrWhiteSpace(uri.Host)) return false;

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var ip) && IsPrivate(ip))
        {
            return false;
        }

        return true;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10
                || b[0] == 127
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31);
        }

        return false;
    }

    private sealed class LimitedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _limit;
        private int _read;

        public LimitedReadStream(Stream inner, int limit)
        {
            _inner = inner;
            _limit = limit;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_read >= _limit) return 0;
            var remaining = Math.Min(count, _limit - _read);
            var n = _inner.Read(buffer, offset, remaining);
            _read += n;
            return n;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_read >= _limit) return 0;
            var remaining = Math.Min(buffer.Length, _limit - _read);
            var n = await _inner.ReadAsync(buffer[..remaining], cancellationToken);
            _read += n;
            return n;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public static class FacebookLinkMetadata
{
    public const string StoredFilePrefix = "news-file/";

    public static bool IsStoredFile(string? thumbnail)
    {
        return !string.IsNullOrWhiteSpace(thumbnail)
            && thumbnail.Trim().StartsWith(StoredFilePrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string? StorageKey(string? thumbnail)
    {
        if (!IsStoredFile(thumbnail)) return null;
        var key = thumbnail!.Trim()[StoredFilePrefix.Length..];
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    public static bool IsFacebook(string? pageUrl)
    {
        if (!Uri.TryCreate(pageUrl?.Trim(), UriKind.Absolute, out var uri)) return false;
        return IsFacebookHost(uri.Host);
    }

    public static bool IsPageLevel(string? pageUrl)
    {
        if (!IsFacebook(pageUrl)) return false;
        if (!Uri.TryCreate(pageUrl?.Trim(), UriKind.Absolute, out var uri)) return false;

        var path = uri.AbsolutePath;
        if (path.Contains("/groups/", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains("/share/g/", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains("/photo", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(path, "/photo.php", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool IsRemoteFacebookImage(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)) return false;
        var host = uri.Host;
        return host.Contains("facebook.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fb.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fb.watch", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fbsbx.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".fbcdn.net", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFacebookHost(string host)
    {
        return host.Contains("facebook.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fb.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("fb.watch", StringComparison.OrdinalIgnoreCase);
    }
}

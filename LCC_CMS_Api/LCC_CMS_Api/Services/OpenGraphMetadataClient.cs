using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LCC_CMS_Api.Services;

public sealed record OpenGraphMetadata(
    string? Title,
    string? Description,
    string? ImageUrl);

public sealed class OpenGraphMetadataClient
{
    private const int MaxBytes = 512_000;
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
        using var response = await _http.GetAsync(oembed, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

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
        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

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

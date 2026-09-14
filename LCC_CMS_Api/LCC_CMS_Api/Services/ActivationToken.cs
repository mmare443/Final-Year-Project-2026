using System.Security.Cryptography;

namespace LCC_CMS_Api.Services;

public static class ActivationToken
{
    public const int ExpiryDays = 7;

    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static DateTime ExpiresAtUtc() => DateTime.UtcNow.AddDays(ExpiryDays);

    public static string ActivationLink(string? baseUrl, string token)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://lccbportal.org/activate"
            : baseUrl.Trim().TrimEnd('/');
        return $"{root}?token={Uri.EscapeDataString(token)}";
    }
}

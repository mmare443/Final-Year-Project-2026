using System.Security.Cryptography;

namespace LCC_CMS_Api.Services;

public static class PasswordReset
{
    public const int ExpiryMinutes = 60;

    public static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static DateTime ExpiresAtUtc() => DateTime.UtcNow.AddMinutes(ExpiryMinutes);

    public static string ResetLink(string? baseUrl, string token)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://localhost:5173/reset-password"
            : baseUrl.Trim().TrimEnd('/');
        return $"{root}?token={Uri.EscapeDataString(token)}";
    }
}

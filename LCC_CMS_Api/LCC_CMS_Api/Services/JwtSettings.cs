namespace LCC_CMS_Api.Services;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "LCC_CMS_Api";
    public string Audience { get; set; } = "LCC_CMS_Web";
    public int ExpiryMinutes { get; set; } = 480;

    /// <summary>
    /// Development only. When set, active users with a null password_hash
    /// receive this password at startup so local login can be tested.
    /// </summary>
    public string? LabPassword { get; set; }
}

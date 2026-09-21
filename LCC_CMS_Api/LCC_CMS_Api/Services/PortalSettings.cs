namespace LCC_CMS_Api.Services;

public sealed class PortalSettings
{
    public const string SectionName = "Portal";

    public string ActivationBaseUrl { get; set; } = "https://lccbportal.org/activate";

    public string ResetPasswordBaseUrl { get; set; } = "http://localhost:5173/reset-password";
}

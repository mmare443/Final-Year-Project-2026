namespace LCC_CMS_Api.Services;

public sealed class PortalSettings
{
    public const string SectionName = "Portal";

    public string ActivationBaseUrl { get; set; } = "http://portal.lccbportal.org/activate";

    public string ResetPasswordBaseUrl { get; set; } = "http://portal.lccbportal.org/reset-password";
}

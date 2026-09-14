using LCC_CMS_Api.Models;

namespace LCC_CMS_Api.Services;

public static class OnboardingStatus
{
    public const string Applied = "Applied";
    public const string Approved = "Approved";
    public const string TokenGenerated = "Token Generated";
    public const string Activated = "Activated";
    public const string Enrolled = "Enrolled";
    public const string Rejected = "Rejected";

    public static string From(Admission admission)
    {
        if (string.Equals(admission.Status, "Applied", StringComparison.OrdinalIgnoreCase))
        {
            return Applied;
        }

        if (string.Equals(admission.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return Rejected;
        }

        var student = admission.Student;
        var user = student?.StudentNavigation;
        if (user is null)
        {
            return Approved;
        }

        var enrolled = string.Equals(
            student?.EnrolmentStatus,
            "Enrolled",
            StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(user.ActivationToken) && !user.ActivationUsed)
        {
            return TokenGenerated;
        }

        if (user.ActivationUsed)
        {
            return enrolled ? Enrolled : Activated;
        }

        if (!string.IsNullOrEmpty(user.PasswordHash) && enrolled)
        {
            return Enrolled;
        }

        return Approved;
    }
}

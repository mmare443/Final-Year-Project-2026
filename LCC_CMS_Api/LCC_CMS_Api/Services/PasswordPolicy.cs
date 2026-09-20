using System.Text;

namespace LCC_CMS_Api.Services;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public const string Requirements =
        "Password must be at least 8 characters and include uppercase, lowercase, a number, and a special character.";

    public static string? Validate(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            return Requirements;
        }

        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        var hasSpecial = false;
        foreach (var ch in password)
        {
            if (char.IsUpper(ch)) hasUpper = true;
            else if (char.IsLower(ch)) hasLower = true;
            else if (char.IsDigit(ch)) hasDigit = true;
            else hasSpecial = true;
        }

        if (!hasUpper || !hasLower || !hasDigit || !hasSpecial)
        {
            return Requirements;
        }

        return null;
    }

    public static bool EqualsTemporary(string password, string? labPassword)
    {
        return !string.IsNullOrEmpty(labPassword)
            && string.Equals(password, labPassword, StringComparison.Ordinal);
    }

    public static string Describe()
    {
        var sb = new StringBuilder();
        sb.Append(Requirements);
        return sb.ToString();
    }
}

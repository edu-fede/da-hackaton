namespace Hackaton.Api.Auth;

/// <summary>Server-side password rules, per Decisions §5 in `docs/stories.md`: ≥8 chars, ≥1 letter, ≥1 digit.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static bool TryValidate(string? password, out string? error)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            error = $"Password must be at least {MinLength} characters.";
            return false;
        }

        var hasLetter = false;
        var hasDigit = false;
        foreach (var ch in password)
        {
            if (char.IsLetter(ch)) hasLetter = true;
            else if (char.IsDigit(ch)) hasDigit = true;
            if (hasLetter && hasDigit) break;
        }

        if (!hasLetter || !hasDigit)
        {
            error = "Password must contain at least one letter and one digit.";
            return false;
        }

        error = null;
        return true;
    }
}

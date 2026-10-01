namespace PamRdpProxyManager.Core.Services;

/// <summary>
/// Builds the proxy user name, always in the format <c>user#confirmidtoken#rdphost</c>.
/// Without a token the segment stays empty: <c>user##rdphost</c>.
/// </summary>
public static class UsernameBuilder
{
    public const char Separator = '#';

    public static string Build(string user, string? token, string targetHost)
    {
        var error = Validate(user, token, targetHost);
        if (error is not null)
        {
            throw new ArgumentException(error);
        }

        return $"{user.Trim()}{Separator}{token?.Trim()}{Separator}{targetHost.Trim()}";
    }

    /// <summary>Returns a user-facing error message or <c>null</c> if all parts are valid.</summary>
    public static string? Validate(string? user, string? token, string? targetHost)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return "Bitte einen Benutzernamen angeben.";
        }

        if (ContainsInvalidChars(user))
        {
            return "Der Benutzername darf weder '#' noch Leer- oder Steuerzeichen enthalten.";
        }

        if (!string.IsNullOrWhiteSpace(token) && ContainsInvalidChars(token))
        {
            return "Das Confirm-ID-Token darf weder '#' noch Leer- oder Steuerzeichen enthalten.";
        }

        return ValidateTarget(targetHost);
    }

    public static string? ValidateTarget(string? targetHost)
    {
        if (string.IsNullOrWhiteSpace(targetHost))
        {
            return "Bitte einen Zielserver angeben.";
        }

        if (ContainsInvalidChars(targetHost))
        {
            return "Der Zielserver darf weder '#' noch Leer- oder Steuerzeichen enthalten.";
        }

        return null;
    }

    private static bool ContainsInvalidChars(string value) =>
        value.Trim().Any(c => c == Separator || char.IsWhiteSpace(c) || char.IsControl(c));
}

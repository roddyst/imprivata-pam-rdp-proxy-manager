using PamRdpProxyManager.Core.Models;

namespace PamRdpProxyManager.Core.Services;

/// <summary>Builds the proxy user name in the format <c>user#confirmidtoken#rdphost</c>.</summary>
public static class UsernameBuilder
{
    public const char Separator = '#';

    public static string Build(string user, string? token, string targetHost, EmptyTokenFormat emptyTokenFormat = EmptyTokenFormat.KeepEmptySegment)
    {
        var error = Validate(user, token, targetHost);
        if (error is not null)
        {
            throw new ArgumentException(error);
        }

        user = user.Trim();
        token = token?.Trim() ?? string.Empty;
        targetHost = targetHost.Trim();

        if (token.Length == 0 && emptyTokenFormat == EmptyTokenFormat.OmitSegment)
        {
            return $"{user}{Separator}{targetHost}";
        }

        return $"{user}{Separator}{token}{Separator}{targetHost}";
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

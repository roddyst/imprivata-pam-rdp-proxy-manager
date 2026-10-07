using System.Diagnostics.CodeAnalysis;

namespace PamRdpProxyManager.Core.Services;

/// <summary>Turns user input such as <c>https://pam.example.com/</c> or <c>pam.example.com:3390</c> into a host (and optional port).</summary>
public static class ProxyHostParser
{
    public static bool TryParse(string? input, [NotNullWhen(true)] out string? host, out int? port, out string? error)
    {
        host = null;
        port = null;
        error = null;

        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            error = "Es wurde kein Server angegeben.";
            return false;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            // Uri needs a scheme to parse "host:port" and IPv6 literals reliably.
            text = "rdp://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            error = "Ungültiger Hostname oder URL.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "Die Adresse darf keine Zugangsdaten enthalten.";
            return false;
        }

        var name = uri.HostNameType == UriHostNameType.IPv6 ? uri.Host.Trim('[', ']') : uri.Host;
        if (Uri.CheckHostName(name) == UriHostNameType.Unknown)
        {
            error = "Ungültiger Hostname.";
            return false;
        }

        // Internationalized names are used in their ASCII (punycode) form, which is also what DNS resolves. A
        // look-alike such as "pаm.example.com" with a Cyrillic "а" then shows up as "xn--…" instead of passing as
        // the real server in the settings check.
        host = uri.HostNameType switch
        {
            UriHostNameType.IPv6 => $"[{name}]",
            UriHostNameType.Dns => uri.IdnHost,
            _ => name,
        };

        // Only take the port from "host:port" input; a URL's default (e.g. 443 for https) is not an RDP port.
        if (!uri.IsDefaultPort && uri.Port > 0 && text.StartsWith("rdp://", StringComparison.OrdinalIgnoreCase))
        {
            port = uri.Port;
        }

        return true;
    }

    public static bool IsValidPort(int port) => port is > 0 and <= 65535;
}

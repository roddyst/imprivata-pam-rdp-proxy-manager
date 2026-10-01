using System.Globalization;
using System.Text;
using PamRdpProxyManager.Core.Models;

namespace PamRdpProxyManager.Core.Services;

/// <summary>
/// Generates the content of a temporary .rdp file. The file never contains a password, token or user name;
/// mstsc picks the credentials up from the temporary <c>TERMSRV/&lt;host&gt;</c> credential instead.
/// </summary>
public static class RdpFileBuilder
{
    /// <summary>Settings that must never be taken from <see cref="RdpOptions.AdditionalSettings"/>.</summary>
    private static readonly string[] BlockedKeys =
    [
        "password 51",
        "username",
        "domain",
        "full address",
        "alternate full address",
        "server port",
        "prompt for credentials",
        "promptcredentialonce",
        "kdcproxyname",

        // Have dedicated controls; must not be weakened silently (e.g. by a manipulated settings.json).
        "authentication level",
        "enablecredsspsupport",
    ];

    /// <summary>
    /// An RD gateway could receive the credentials meant for the PAM server, so no gateway settings are accepted.
    /// </summary>
    private const string BlockedKeyPrefix = "gateway";

    // Fixed list instead of Path.GetInvalidFileNameChars(), which is platform dependent.
    private static readonly char[] InvalidFileNameChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly string[] ReservedFileNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    public static string Build(string proxyHost, int port, RdpOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proxyHost);
        if (!ProxyHostParser.IsValidPort(port))
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535.");
        }

        var lines = new List<string>
        {
            // Always explicit – the proxy does not listen on the mstsc default port 3389.
            S("full address", $"{proxyHost}:{port}"),
            I("prompt for credentials", 0),
            I("screen mode id", options.DisplayMode == DisplayMode.Fullscreen ? 2 : 1),
        };

        if (options.DisplayMode == DisplayMode.Windowed)
        {
            lines.Add(I("desktopwidth", Math.Clamp(options.DesktopWidth, 640, 8192)));
            lines.Add(I("desktopheight", Math.Clamp(options.DesktopHeight, 480, 8192)));
        }

        lines.Add(I("use multimon", options.UseMultiMonitor));
        lines.Add(I("smart sizing", options.SmartSizing));
        lines.Add(I("dynamic resolution", options.DynamicResolution));
        lines.Add(I("session bpp", NormalizeColorDepth(options.ColorDepth)));
        lines.Add(I("redirectclipboard", options.RedirectClipboard));
        lines.Add(S("drivestoredirect", options.RedirectDrives ? "*" : string.Empty));
        lines.Add(I("redirectprinters", options.RedirectPrinters));
        lines.Add(I("audiomode", (int)options.AudioMode));
        lines.Add(I("audiocapturemode", options.AudioCapture));
        lines.Add(I("enablecredsspsupport", options.EnableNla));
        lines.Add(I("authentication level", (int)options.AuthenticationLevel));
        lines.Add(I("autoreconnection enabled", options.AutoReconnect));

        foreach (var extra in ParseAdditionalSettings(options.AdditionalSettings))
        {
            // Later lines win in mstsc, so replace an existing key instead of duplicating it.
            var key = KeyOf(extra);
            lines.RemoveAll(l => string.Equals(KeyOf(l), key, StringComparison.OrdinalIgnoreCase));
            lines.Add(extra);
        }

        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.Append(line).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>Returns valid, non-blocked <c>key:type:value</c> lines from the free-text field.</summary>
    public static IEnumerable<string> ParseAdditionalSettings(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var parts = line.Split(':', 3);
            if (parts.Length != 3 || parts[0].Trim().Length == 0 || parts[1] is not ("i" or "s" or "b"))
            {
                continue;
            }

            var key = parts[0].Trim();
            if (BlockedKeys.Contains(key, StringComparer.OrdinalIgnoreCase) || key.StartsWith(BlockedKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return line;
        }
    }

    /// <summary>
    /// Returns a Windows-safe .rdp file name for <paramref name="targetHost"/>. mstsc shows the file name
    /// in its window title and therefore in the taskbar, so it should name the target server.
    /// </summary>
    public static string FileNameFor(string? targetHost)
    {
        const int maxLength = 100;
        var name = new string((targetHost ?? string.Empty).Trim()
            .Select(c => c < 32 || InvalidFileNameChars.Contains(c) ? '_' : c)
            .ToArray());
        if (name.Length > maxLength)
        {
            name = name[..maxLength];
        }

        // Windows silently drops trailing dots and spaces and reserves device names such as CON or COM1.
        name = name.TrimEnd('.', ' ');
        if (name.Length == 0)
        {
            name = "Remotedesktop";
        }
        else if (ReservedFileNames.Contains(name.Split('.')[0], StringComparer.OrdinalIgnoreCase))
        {
            name = "_" + name;
        }

        return name + ".rdp";
    }

    internal static int NormalizeColorDepth(int bpp) => bpp switch
    {
        15 or 16 or 24 or 32 => bpp,
        _ => 32,
    };

    private static string KeyOf(string line) => line.Split(':', 2)[0].Trim();

    private static string S(string key, string value) => $"{key}:s:{value}";

    private static string I(string key, int value) => $"{key}:i:{value.ToString(CultureInfo.InvariantCulture)}";

    private static string I(string key, bool value) => I(key, value ? 1 : 0);
}

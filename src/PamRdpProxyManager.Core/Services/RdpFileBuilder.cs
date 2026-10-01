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
        "gatewaycredentialssource",
        "gatewayusername",
        "gatewaypassword",
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
            S("full address", port == ConnectionProfile.DefaultRdpPort ? proxyHost : $"{proxyHost}:{port}"),
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

            if (BlockedKeys.Contains(parts[0].Trim(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return line;
        }
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

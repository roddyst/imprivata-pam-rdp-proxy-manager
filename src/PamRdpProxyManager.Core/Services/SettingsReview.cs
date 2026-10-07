using System.Globalization;
using System.Text;
using PamRdpProxyManager.Core.Models;

namespace PamRdpProxyManager.Core.Services;

/// <summary>
/// Describes the security-relevant parts of a settings file that was not saved by this app, so the user can decide
/// whether to trust it. Shows what would actually be used (the parsed PAM server, the effective additional .rdp
/// lines) instead of the raw text, which could hide the real server behind line breaks or look-alike characters.
/// </summary>
public static class SettingsReview
{
    private const int MaxDisplayLength = 80;

    public static string Describe(AppSettings settings)
    {
        var text = new StringBuilder();
        foreach (var profile in settings.Profiles)
        {
            text.Append("  •  ").Append(Sanitize(profile.Name)).Append(": ");
            if (ProxyHostParser.TryParse(profile.ProxyHost, out var host, out var port, out _))
            {
                text.Append(host).Append(':').Append(port ?? profile.Port);
            }
            else
            {
                text.Append("ungültige Adresse – Verbinden nicht möglich");
            }

            var rdp = profile.Rdp;
            if (rdp is null)
            {
                text.AppendLine();
                continue;
            }

            if (rdp.RedirectDrives)
            {
                text.Append("  (Laufwerke werden umgeleitet)");
            }

            if (!rdp.EnableNla)
            {
                text.Append("  (NLA ausgeschaltet)");
            }

            text.AppendLine();
            foreach (var line in RdpFileBuilder.ParseAdditionalSettings(rdp.AdditionalSettings))
            {
                text.Append("        zusätzlich: ").AppendLine(Sanitize(line));
            }
        }

        return text.ToString();
    }

    /// <summary>Removes control and invisible formatting characters and shortens long values for display.</summary>
    public static string Sanitize(string? value)
    {
        var clean = new string((value ?? string.Empty)
            .Where(c => !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) is not (UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator))
            .ToArray())
            .Trim();
        return clean.Length > MaxDisplayLength ? clean[..MaxDisplayLength] + "…" : clean;
    }
}

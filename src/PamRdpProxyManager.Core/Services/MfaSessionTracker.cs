using System.Security.Cryptography;
using System.Text;
using PamRdpProxyManager.Core.Models;

namespace PamRdpProxyManager.Core.Services;

/// <summary>
/// Tracks how long a confirm ID token entered for a PAM server stays valid. Within that time the PAM server
/// does not need the token again, so connections use <c>user##rdphost</c>.
/// </summary>
public static class MfaSessionTracker
{
    /// <summary>Entries older than this are removed (longer than the maximum validity).</summary>
    private static readonly TimeSpan Retention = TimeSpan.FromHours(AppSettings.MaxMfaValidityHours + 24);

    /// <summary>Tolerated clock difference before a start time in the future is considered invalid.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public static TimeSpan Validity(AppSettings settings) =>
        TimeSpan.FromHours(Math.Clamp(settings.MfaValidityHours, AppSettings.MinMfaValidityHours, AppSettings.MaxMfaValidityHours));

    /// <summary>Stable, non-reversible id for the combination of user name and PAM server (case-insensitive).</summary>
    public static string IdFor(string userName, string proxyHost)
    {
        var key = $"{userName.Trim().ToUpperInvariant()}\n{proxyHost.Trim().ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32].ToLowerInvariant();
    }

    /// <summary>
    /// Returns when the token was entered for this user and PAM server, or <c>null</c> if the feature is disabled
    /// or nothing (plausible) is recorded. The result may lie in the past beyond the validity (= expired).
    /// </summary>
    public static DateTimeOffset? EnteredAt(AppSettings settings, string userName, string proxyHost, DateTimeOffset now)
    {
        if (!settings.MfaValidityEnabled)
        {
            return null;
        }

        var id = IdFor(userName, proxyHost);
        var entry = settings.MfaSessions.FirstOrDefault(s => s.Id == id);

        // A start time in the future means the clock was changed – do not trust it.
        return entry is null || entry.TokenEnteredAt > now + ClockSkew ? null : entry.TokenEnteredAt;
    }

    /// <summary>End of the validity, or <c>null</c> if no token was entered (or the feature is disabled).</summary>
    public static DateTimeOffset? ExpiresAt(AppSettings settings, string userName, string proxyHost, DateTimeOffset now) =>
        EnteredAt(settings, userName, proxyHost, now) is { } enteredAt ? enteredAt + Validity(settings) : null;

    /// <summary><c>true</c> if a token was entered and is still valid, i.e. it must not be sent again.</summary>
    public static bool IsValid(AppSettings settings, string userName, string proxyHost, DateTimeOffset now) =>
        ExpiresAt(settings, userName, proxyHost, now) is { } expiresAt && now < expiresAt;

    /// <summary>Records that a token has just been sent – the validity (re)starts now.</summary>
    public static void Start(AppSettings settings, string userName, string proxyHost, DateTimeOffset now)
    {
        var id = IdFor(userName, proxyHost);
        settings.MfaSessions.RemoveAll(s => s.Id == id);
        settings.MfaSessions.Add(new MfaSession { Id = id, TokenEnteredAt = now });
        Prune(settings, now);
    }

    /// <summary>Forgets the token for this user and PAM server, so the next connection asks for a new one.</summary>
    public static bool Reset(AppSettings settings, string userName, string proxyHost) =>
        settings.MfaSessions.RemoveAll(s => s.Id == IdFor(userName, proxyHost)) > 0;

    /// <summary>Removes invalid and long expired entries.</summary>
    public static void Prune(AppSettings settings, DateTimeOffset now) =>
        settings.MfaSessions.RemoveAll(s =>
            s is null
            || string.IsNullOrWhiteSpace(s.Id)
            || s.TokenEnteredAt > now + ClockSkew
            || s.TokenEnteredAt < now - Retention);
}

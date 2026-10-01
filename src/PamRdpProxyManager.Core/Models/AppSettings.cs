namespace PamRdpProxyManager.Core.Models;

/// <summary>Root object of the portable <c>settings.json</c>. Never contains passwords or tokens.</summary>
public class AppSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<ConnectionProfile> Profiles { get; set; } = [];

    public string? ActiveProfileName { get; set; }

    public List<RecentTarget> RecentTargets { get; set; } = [];

    /// <summary>Maximum number of non-favorite recent targets that are kept.</summary>
    public int MaxRecentTargets { get; set; } = 20;

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public bool RememberUserName { get; set; } = true;

    /// <summary>Last user name (only stored when <see cref="RememberUserName"/> is set). Not a secret.</summary>
    public string? LastUserName { get; set; }

    /// <summary>Clear the confirm ID token after each connection (tokens are usually single-use).</summary>
    public bool ClearTokenAfterConnect { get; set; } = true;

    /// <summary>
    /// Seconds after starting mstsc until the temporary credential and .rdp file are removed.
    /// mstsc reads both while establishing the connection.
    /// </summary>
    public int CredentialCleanupDelaySeconds { get; set; } = 15;

    public const int MinMfaValidityHours = 1;
    public const int MaxMfaValidityHours = 168;

    /// <summary>
    /// The PAM server accepts a confirm ID token for <see cref="MfaValidityHours"/>. While it is valid the token is
    /// not sent again (<c>user##rdphost</c>); afterwards the app asks for a new one.
    /// </summary>
    public bool MfaValidityEnabled { get; set; } = true;

    public int MfaValidityHours { get; set; } = 8;

    /// <summary>When a token was entered per user and PAM server (hashed id, no token).</summary>
    public List<MfaSession> MfaSessions { get; set; } = [];

    /// <summary>Migrates older files and ensures there is at least one profile and a valid active profile.</summary>
    public void Normalize()
    {
        if (SchemaVersion < 2)
        {
            // Version 1 used the mstsc default 3389 as profile default; the app default is now 3388.
            foreach (var profile in Profiles.Where(p => p?.Port == 3389))
            {
                profile.Port = ConnectionProfile.DefaultRdpPort;
            }
        }

        SchemaVersion = CurrentSchemaVersion;

        Profiles.RemoveAll(p => p is null);
        if (Profiles.Count == 0)
        {
            Profiles.Add(new ConnectionProfile { Name = "Standard", ProxyHost = "pam.example.com" });
        }

        if (ActiveProfileName is null || Profiles.All(p => p.Name != ActiveProfileName))
        {
            ActiveProfileName = Profiles[0].Name;
        }

        RecentTargets.RemoveAll(t => t is null || string.IsNullOrWhiteSpace(t.Host));
        MaxRecentTargets = Math.Clamp(MaxRecentTargets, 1, 200);
        CredentialCleanupDelaySeconds = Math.Clamp(CredentialCleanupDelaySeconds, 5, 300);
        MfaValidityHours = Math.Clamp(MfaValidityHours, MinMfaValidityHours, MaxMfaValidityHours);
        MfaSessions ??= [];
        Services.MfaSessionTracker.Prune(this, DateTimeOffset.Now);
    }
}

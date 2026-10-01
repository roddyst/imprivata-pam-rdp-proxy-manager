namespace PamRdpProxyManager.Core.Models;

/// <summary>Root object of the portable <c>settings.json</c>. Never contains passwords or tokens.</summary>
public class AppSettings
{
    public const int CurrentSchemaVersion = 1;

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

    public EmptyTokenFormat EmptyTokenFormat { get; set; } = EmptyTokenFormat.KeepEmptySegment;

    /// <summary>Clear the confirm ID token after each connection (tokens are usually single-use).</summary>
    public bool ClearTokenAfterConnect { get; set; } = true;

    /// <summary>
    /// Seconds after starting mstsc until the temporary credential and .rdp file are removed.
    /// mstsc reads both while establishing the connection.
    /// </summary>
    public int CredentialCleanupDelaySeconds { get; set; } = 15;

    /// <summary>Ensures there is at least one profile and the active profile name is valid.</summary>
    public void Normalize()
    {
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
    }
}

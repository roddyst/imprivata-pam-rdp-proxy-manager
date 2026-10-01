using CommunityToolkit.Mvvm.ComponentModel;

namespace PamRdpProxyManager.Core.Models;

/// <summary>A previously used target server (rdphost).</summary>
public partial class RecentTarget : ObservableObject
{
    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _lastUsed = DateTimeOffset.Now;

    [ObservableProperty]
    private bool _isFavorite;

    /// <summary>Name of the profile last used with this target (optional).</summary>
    [ObservableProperty]
    private string? _profileName;
}

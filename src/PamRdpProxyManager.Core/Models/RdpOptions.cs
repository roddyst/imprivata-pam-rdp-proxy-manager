using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PamRdpProxyManager.Core.Models;

/// <summary>Options written to the generated .rdp file.</summary>
public partial class RdpOptions : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWindowed))]
    private DisplayMode _displayMode = DisplayMode.Fullscreen;

    [JsonIgnore]
    public bool IsWindowed => DisplayMode == DisplayMode.Windowed;

    [ObservableProperty]
    private int _desktopWidth = 1920;

    [ObservableProperty]
    private int _desktopHeight = 1080;

    [ObservableProperty]
    private bool _useMultiMonitor;

    [ObservableProperty]
    private bool _smartSizing = true;

    [ObservableProperty]
    private bool _dynamicResolution = true;

    /// <summary>Color depth in bits per pixel (15, 16, 24 or 32).</summary>
    [ObservableProperty]
    private int _colorDepth = 32;

    [ObservableProperty]
    private bool _redirectClipboard = true;

    [ObservableProperty]
    private bool _redirectDrives;

    [ObservableProperty]
    private bool _redirectPrinters;

    [ObservableProperty]
    private AudioMode _audioMode = AudioMode.PlayLocally;

    [ObservableProperty]
    private bool _audioCapture;

    /// <summary>Network Level Authentication (CredSSP).</summary>
    [ObservableProperty]
    private bool _enableNla = true;

    [ObservableProperty]
    private ServerAuthenticationLevel _authenticationLevel = ServerAuthenticationLevel.Warn;

    [ObservableProperty]
    private bool _autoReconnect = true;

    /// <summary>
    /// Additional raw .rdp lines (one per line, e.g. <c>keyboardhook:i:2</c>).
    /// Lines that would carry credentials are filtered out when the file is generated.
    /// </summary>
    [ObservableProperty]
    private string _additionalSettings = string.Empty;

    // JSON round-trip instead of MemberwiseClone, which would also copy PropertyChanged subscribers.
    public RdpOptions Clone() => JsonClone.Of(this);
}

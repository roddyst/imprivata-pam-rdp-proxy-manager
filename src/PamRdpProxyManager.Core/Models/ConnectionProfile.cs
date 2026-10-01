using CommunityToolkit.Mvvm.ComponentModel;

namespace PamRdpProxyManager.Core.Models;

/// <summary>A named set of proxy and RDP settings.</summary>
public partial class ConnectionProfile : ObservableObject
{
    /// <summary>Default RDP port of the PAM RDP proxy used by this app.</summary>
    public const int DefaultRdpPort = 3388;

    [ObservableProperty]
    private string _name = "Standard";

    /// <summary>Host name or URL of the PAM server acting as RDP proxy, e.g. <c>pam.example.com</c>.</summary>
    [ObservableProperty]
    private string _proxyHost = string.Empty;

    [ObservableProperty]
    private int _port = DefaultRdpPort;

    [ObservableProperty]
    private RdpOptions _rdp = new();

    public ConnectionProfile Clone(string? newName = null)
    {
        var copy = JsonClone.Of(this);
        copy.Name = newName ?? Name;
        return copy;
    }

    public override string ToString() => Name;
}

namespace PamRdpProxyManager.Core.Models;

/// <summary>How the remote session window is displayed.</summary>
public enum DisplayMode
{
    Fullscreen,
    Windowed,
}

/// <summary>Maps to the <c>audiomode</c> RDP setting.</summary>
public enum AudioMode
{
    /// <summary>Play audio on this computer (audiomode:i:0).</summary>
    PlayLocally = 0,

    /// <summary>Play audio on the remote computer (audiomode:i:1).</summary>
    PlayRemotely = 1,

    /// <summary>Do not play audio (audiomode:i:2).</summary>
    DoNotPlay = 2,
}

/// <summary>Maps to the <c>authentication level</c> RDP setting (server authentication).</summary>
public enum ServerAuthenticationLevel
{
    /// <summary>Connect without warning if server authentication fails.</summary>
    ConnectWithoutWarning = 0,

    /// <summary>Do not connect if server authentication fails.</summary>
    DoNotConnect = 1,

    /// <summary>Warn if server authentication fails.</summary>
    Warn = 2,
}

/// <summary>How the user name is built when no confirm ID token was entered.</summary>
public enum EmptyTokenFormat
{
    /// <summary><c>user##rdphost</c> – keeps the empty token segment (default).</summary>
    KeepEmptySegment,

    /// <summary><c>user#rdphost</c> – drops the token segment entirely.</summary>
    OmitSegment,
}

public enum AppTheme
{
    Dark,
    Light,
    System,
}

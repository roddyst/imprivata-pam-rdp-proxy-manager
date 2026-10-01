namespace PamRdpProxyManager.Core.Services;

/// <summary>
/// Creates and checks a signature for <c>settings.json</c>, so changes made outside the app (e.g. a different
/// PAM server that would receive the password) are noticed. The Windows app uses DPAPI for the current user.
/// </summary>
public interface ISettingsProtector
{
    byte[] Sign(byte[] content);

    /// <summary>Returns <c>false</c> for a signature that does not match or cannot be checked (never throws).</summary>
    bool Verify(byte[] content, byte[] signature);
}

/// <summary>Result of the signature check when loading <c>settings.json</c>.</summary>
public enum SettingsVerification
{
    /// <summary>No settings file (or an unreadable one) – defaults are used, nothing to verify.</summary>
    NoFile,

    /// <summary>No protector configured.</summary>
    NotChecked,

    /// <summary>The file was saved by this app for the current user.</summary>
    Verified,

    /// <summary>
    /// Missing or invalid signature: first start after an update, a copy from another computer or user – or a
    /// change made outside the app. The settings must be confirmed by the user before they are used.
    /// </summary>
    Unverified,
}

namespace PamRdpProxyManager.Core.Models;

/// <summary>
/// Remembers when a confirm ID token was last sent to a PAM server for a user. Contains no token and no
/// readable user name – <see cref="Id"/> is a hash of user name and PAM server.
/// </summary>
public class MfaSession
{
    public string Id { get; set; } = string.Empty;

    /// <summary>When the token was entered (sent to the PAM server). The MFA validity starts here.</summary>
    public DateTimeOffset TokenEnteredAt { get; set; }
}

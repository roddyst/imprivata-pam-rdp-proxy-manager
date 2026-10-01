using System.Security;

namespace PamRdpProxyManager.Services;

/// <summary>
/// Credentials entered at startup. Kept in memory only; the password stays in a <see cref="SecureString"/>
/// and is never converted to a managed string.
/// </summary>
public sealed class UserSession : IDisposable
{
    public UserSession(string userName, SecureString password, string? token)
    {
        UserName = userName.Trim();
        Password = password.Copy();
        Password.MakeReadOnly();
        Token = token?.Trim() ?? string.Empty;
    }

    public string UserName { get; }

    public SecureString Password { get; }

    /// <summary>Confirm ID token entered in the login dialog (optional, may be replaced per connection).</summary>
    public string Token { get; }

    public void Dispose() => Password.Dispose();
}

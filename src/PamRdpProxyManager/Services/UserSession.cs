using System.Security;

namespace PamRdpProxyManager.Services;

/// <summary>
/// Credentials entered at startup. Kept in memory only; the password stays in a <see cref="SecureString"/>
/// and is never converted to a managed string.
/// </summary>
public sealed class UserSession : IDisposable
{
    public UserSession(string userName, SecureString password)
    {
        UserName = userName.Trim();
        Password = password.Copy();
        Password.MakeReadOnly();
    }

    public string UserName { get; }

    public SecureString Password { get; }

    public void Dispose() => Password.Dispose();
}

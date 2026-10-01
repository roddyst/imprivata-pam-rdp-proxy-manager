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
        _token = token?.Trim() ?? string.Empty;
    }

    private string? _token;

    public string UserName { get; }

    public SecureString Password { get; }

    /// <summary>
    /// Returns the confirm ID token entered in the login dialog (may be empty) and drops the session's reference,
    /// so the token is not kept for the whole session.
    /// </summary>
    public string TakeToken()
    {
        var token = _token ?? string.Empty;
        _token = null;
        return token;
    }

    public void Dispose() => Password.Dispose();
}

using System.Security.Cryptography;
using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Services;

/// <summary>
/// Signs <c>settings.json</c> with DPAPI for the current Windows user: only this user on this computer can create a
/// valid signature, so a settings file placed or changed by someone else (e.g. on a shared drive) is detected.
/// </summary>
public sealed class DpapiSettingsProtector : ISettingsProtector
{
    private static readonly byte[] Entropy = "ImprivataPamRdpProxyManager/settings.sig/v1"u8.ToArray();

    public byte[] Sign(byte[] content) =>
        ProtectedData.Protect(SHA256.HashData(content), Entropy, DataProtectionScope.CurrentUser);

    public bool Verify(byte[] content, byte[] signature)
    {
        try
        {
            var expected = ProtectedData.Unprotect(signature, Entropy, DataProtectionScope.CurrentUser);
            return CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(content));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}

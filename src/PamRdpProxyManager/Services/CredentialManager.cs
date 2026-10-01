using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using static PamRdpProxyManager.Services.NativeMethods;

namespace PamRdpProxyManager.Services;

/// <summary>
/// Writes the temporary <c>TERMSRV/&lt;host&gt;</c> credential that mstsc uses for the connection.
/// Equivalent to <c>cmdkey /generic:TERMSRV/&lt;host&gt;</c>, but the password never appears on a command line
/// and the entry is only kept for the logon session (never persisted to disk).
/// </summary>
public static class CredentialManager
{
    /// <summary>Marks entries created by this app so leftovers (e.g. after a crash) can be removed safely.</summary>
    public const string Marker = "Created temporarily by Imprivata PAM RDP Proxy Manager";

    public static string TargetFor(string host) => "TERMSRV/" + host.Trim('[', ']');

    public enum ExistingCredential
    {
        None,
        Generic,
        DomainPassword,
    }

    /// <summary>Detects a credential for the target that was not created by this app.</summary>
    public static ExistingCredential FindForeign(string target)
    {
        foreach (var (type, kind) in new[] { (CRED_TYPE_GENERIC, ExistingCredential.Generic), (CRED_TYPE_DOMAIN_PASSWORD, ExistingCredential.DomainPassword) })
        {
            if (!CredRead(target, type, 0, out var ptr))
            {
                continue;
            }

            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
                if (cred.Comment != Marker)
                {
                    return kind;
                }
            }
            finally
            {
                CredFree(ptr);
            }
        }

        return ExistingCredential.None;
    }

    public static void Write(string target, string userName, SecureString password)
    {
        var blob = IntPtr.Zero;
        try
        {
            // Copy the password straight from the SecureString into unmanaged memory – no managed string is created.
            blob = Marshal.SecureStringToCoTaskMemUnicode(password);
            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                Comment = Marker,
                UserName = userName,
                CredentialBlob = blob,
                CredentialBlobSize = (uint)(password.Length * sizeof(char)),
                Persist = CRED_PERSIST_SESSION,
            };

            if (!CredWrite(ref cred, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Der temporäre Anmeldeeintrag konnte nicht angelegt werden.");
            }
        }
        finally
        {
            if (blob != IntPtr.Zero)
            {
                Marshal.ZeroFreeCoTaskMemUnicode(blob);
            }
        }
    }

    /// <summary>Deletes the generic credential if it was created by this app. Returns <c>false</c> if nothing was deleted.</summary>
    public static bool DeleteOwn(string target)
    {
        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out var ptr))
        {
            return false;
        }

        try
        {
            if (Marshal.PtrToStructure<CREDENTIAL>(ptr).Comment != Marker)
            {
                return false;
            }
        }
        finally
        {
            CredFree(ptr);
        }

        return CredDelete(target, CRED_TYPE_GENERIC, 0);
    }

    /// <summary>
    /// Removes TERMSRV entries left behind by this app (e.g. after a crash). Entries younger than
    /// <paramref name="minAge"/> may belong to another running instance that is still connecting and are kept.
    /// </summary>
    public static int DeleteLeftovers(TimeSpan minAge)
    {
        var cutoff = DateTime.UtcNow - minAge;
        if (!CredEnumerate("TERMSRV/*", 0, out var count, out var list))
        {
            return 0;
        }

        var targets = new List<string>();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var ptr = Marshal.ReadIntPtr(list, i * IntPtr.Size);
                var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
                if (cred.Type == CRED_TYPE_GENERIC && cred.Comment == Marker && LastWrittenUtc(cred) < cutoff)
                {
                    targets.Add(cred.TargetName);
                }
            }
        }
        finally
        {
            CredFree(list);
        }

        return targets.Count(t => CredDelete(t, CRED_TYPE_GENERIC, 0));
    }

    private static DateTime LastWrittenUtc(CREDENTIAL cred) =>
        DateTime.FromFileTimeUtc(((long)cred.LastWritten.dwHighDateTime << 32) | (uint)cred.LastWritten.dwLowDateTime);
}

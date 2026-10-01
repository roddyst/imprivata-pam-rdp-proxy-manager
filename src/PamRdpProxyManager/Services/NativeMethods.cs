using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace PamRdpProxyManager.Services;

internal static class NativeMethods
{
    public const uint CRED_TYPE_GENERIC = 1;
    public const uint CRED_TYPE_DOMAIN_PASSWORD = 2;

    /// <summary>Credential lives only for the current logon session and is never written to disk.</summary>
    public const uint CRED_PERSIST_SESSION = 1;

    public const int ERROR_NOT_FOUND = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredEnumerate(string? filter, uint flags, out uint count, out IntPtr credentials);

    [DllImport("advapi32.dll")]
    public static extern void CredFree(IntPtr buffer);
}

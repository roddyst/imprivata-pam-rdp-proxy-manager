using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PamRdpProxyManager.Core.Models;

namespace PamRdpProxyManager.Core.Services;

/// <summary>
/// Loads and saves <c>settings.json</c> next to the executable (portable mode).
/// Falls back to a per-user folder if the executable's folder is not writable.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    public const string SignatureFileName = "settings.sig";

    private readonly ISettingsProtector? _protector;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public SettingsStore(string portableDirectory, string fallbackDirectory, ISettingsProtector? protector = null)
    {
        _protector = protector;
        PortableDirectory = portableDirectory;
        FallbackDirectory = fallbackDirectory;

        var portableFile = Path.Combine(portableDirectory, FileName);
        if (IsDirectoryWritable(portableDirectory) && (!File.Exists(portableFile) || IsFileWritable(portableFile)))
        {
            Directory = portableDirectory;
            IsPortable = true;
        }
        else
        {
            Directory = fallbackDirectory;
            IsPortable = false;
        }
    }

    /// <summary>Folder of the executable.</summary>
    public string PortableDirectory { get; }

    /// <summary>Per-user fallback folder (e.g. %LOCALAPPDATA%\ImprivataPamRdpProxyManager).</summary>
    public string FallbackDirectory { get; }

    /// <summary>Folder that is actually used.</summary>
    public string Directory { get; }

    /// <summary><c>false</c> if the fallback folder is used because the executable's folder is read-only.</summary>
    public bool IsPortable { get; }

    public string FilePath => Path.Combine(Directory, FileName);

    public string SignatureFilePath => Path.Combine(Directory, SignatureFileName);

    /// <summary>Result of the signature check of the last <see cref="Load"/>.</summary>
    public SettingsVerification Verification { get; private set; } = SettingsVerification.NoFile;

    /// <summary>Creates a store for the running executable.</summary>
    public static SettingsStore ForCurrentProcess(string appFolderName, ISettingsProtector? protector = null)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appFolderName);
        return new SettingsStore(exeDir, fallback, protector);
    }

    /// <summary>Loads the settings. A missing or unreadable file yields defaults; a corrupt file is kept as backup.</summary>
    public AppSettings Load(out string? warning)
    {
        warning = null;
        AppSettings? settings = null;
        Verification = SettingsVerification.NoFile;

        if (File.Exists(FilePath))
        {
            try
            {
                var content = File.ReadAllBytes(FilePath);
                settings = JsonSerializer.Deserialize<AppSettings>(Encoding.UTF8.GetString(content).TrimStart('\uFEFF'), JsonOptions);
                Verification = settings is null ? SettingsVerification.NoFile : Verify(content);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                var backup = FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                try
                {
                    File.Copy(FilePath, backup, overwrite: true);
                    warning = $"Die Einstellungsdatei konnte nicht gelesen werden und wurde nach '{backup}' gesichert. Es werden Standardwerte verwendet.";
                }
                catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException)
                {
                    warning = "Die Einstellungsdatei konnte nicht gelesen werden. Es werden Standardwerte verwendet.";
                }
            }
        }

        settings ??= new AppSettings();
        settings.Normalize();
        return settings;
    }

    public void Save(AppSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(settings, JsonOptions));

        // Write to temp files first so a crash never leaves a half-written file behind.
        WriteAtomically(FilePath, content);
        if (_protector is not null)
        {
            WriteAtomically(SignatureFilePath, _protector.Sign(content));
            Verification = SettingsVerification.Verified;
        }
    }

    /// <summary>Moves an untrusted settings file aside (it is kept for inspection) so defaults are used.</summary>
    public string Quarantine()
    {
        var backup = FilePath + ".untrusted-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Move(FilePath, backup, overwrite: true);
        if (File.Exists(SignatureFilePath))
        {
            File.Delete(SignatureFilePath);
        }

        Verification = SettingsVerification.NoFile;
        return backup;
    }

    private SettingsVerification Verify(byte[] content)
    {
        if (_protector is null)
        {
            return SettingsVerification.NotChecked;
        }

        try
        {
            return File.Exists(SignatureFilePath) && _protector.Verify(content, File.ReadAllBytes(SignatureFilePath))
                ? SettingsVerification.Verified
                : SettingsVerification.Unverified;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SettingsVerification.Unverified;
        }
    }

    private static void WriteAtomically(string path, byte[] content)
    {
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    internal static bool IsDirectoryWritable(string directory)
    {
        try
        {
            if (!System.IO.Directory.Exists(directory))
            {
                return false;
            }

            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsFileWritable(string file)
    {
        try
        {
            using var _ = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

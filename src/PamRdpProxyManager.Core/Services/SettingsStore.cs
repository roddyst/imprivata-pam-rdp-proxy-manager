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

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public SettingsStore(string portableDirectory, string fallbackDirectory)
    {
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

    /// <summary>Creates a store for the running executable.</summary>
    public static SettingsStore ForCurrentProcess(string appFolderName)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appFolderName);
        return new SettingsStore(exeDir, fallback);
    }

    /// <summary>Loads the settings. A missing or unreadable file yields defaults; a corrupt file is kept as backup.</summary>
    public AppSettings Load(out string? warning)
    {
        warning = null;
        AppSettings? settings = null;

        if (File.Exists(FilePath))
        {
            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
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
        var json = JsonSerializer.Serialize(settings, JsonOptions);

        // Write to a temp file first so a crash never leaves a half-written settings.json behind.
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, FilePath, overwrite: true);
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

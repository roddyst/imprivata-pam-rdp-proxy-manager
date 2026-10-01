using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pam-rdp-tests-" + Guid.NewGuid().ToString("N"));

    public SettingsStoreTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void UsesPortableDirectory_WhenWritable()
    {
        var store = new SettingsStore(_root, Path.Combine(_root, "fallback"));
        Assert.True(store.IsPortable);
        Assert.Equal(Path.Combine(_root, SettingsStore.FileName), store.FilePath);
    }

    [Fact]
    public void FallsBack_WhenPortableDirectoryIsNotUsable()
    {
        var fallback = Path.Combine(_root, "fallback");
        var store = new SettingsStore(Path.Combine(_root, "does-not-exist"), fallback);
        Assert.False(store.IsPortable);
        Assert.Equal(fallback, store.Directory);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaultsWithProfile()
    {
        var settings = new SettingsStore(_root, _root).Load(out var warning);
        Assert.Null(warning);
        Assert.Single(settings.Profiles);
        Assert.Equal(settings.Profiles[0].Name, settings.ActiveProfileName);
    }

    [Fact]
    public void NewProfile_DefaultsToPort3388() =>
        Assert.Equal(3388, new ConnectionProfile().Port);

    [Fact]
    public void Load_SchemaV1_MigratesOldDefaultPort3389To3388()
    {
        var store = new SettingsStore(_root, _root);
        File.WriteAllText(store.FilePath, """
            {
              "schemaVersion": 1,
              "profiles": [
                { "name": "A", "proxyHost": "pam.example.com", "port": 3389 },
                { "name": "B", "proxyHost": "pam.example.com", "port": 4000 }
              ]
            }
            """);

        var settings = store.Load(out _);

        Assert.Equal(3388, settings.Profiles[0].Port);
        Assert.Equal(4000, settings.Profiles[1].Port);
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void Load_CurrentSchema_KeepsExplicitPort3389()
    {
        var store = new SettingsStore(_root, _root);
        store.Save(new AppSettings { Profiles = [new ConnectionProfile { Name = "A", Port = 3389 }] });

        Assert.Equal(3389, store.Load(out _).Profiles[0].Port);
    }

    [Fact]
    public void Load_ProfileWithoutPort_UsesDefault3388()
    {
        var store = new SettingsStore(_root, _root);
        File.WriteAllText(store.FilePath, """{ "schemaVersion": 2, "profiles": [ { "name": "A", "proxyHost": "pam.example.com" } ] }""");

        Assert.Equal(3388, store.Load(out _).Profiles[0].Port);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var store = new SettingsStore(_root, _root);
        var settings = new AppSettings
        {
            Profiles = [new ConnectionProfile { Name = "Lab", ProxyHost = "pam.example.com", Port = 3390, Rdp = new RdpOptions { ColorDepth = 16 } }],
            ActiveProfileName = "Lab",
            LastUserName = "jdoe",
        };
        RecentTargetList.Touch(settings, "srv01.example.com", "Lab");

        store.Save(settings);
        var loaded = store.Load(out _);

        Assert.Equal("Lab", loaded.ActiveProfileName);
        Assert.Equal(3390, loaded.Profiles[0].Port);
        Assert.Equal(16, loaded.Profiles[0].Rdp.ColorDepth);
        Assert.Equal("srv01.example.com", loaded.RecentTargets.Single().Host);
        Assert.Equal("jdoe", loaded.LastUserName);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndKeepsBackup()
    {
        var store = new SettingsStore(_root, _root);
        File.WriteAllText(store.FilePath, "{ not json");

        var settings = store.Load(out var warning);

        Assert.NotNull(warning);
        Assert.Single(settings.Profiles);
        Assert.Single(Directory.GetFiles(_root, SettingsStore.FileName + ".corrupt-*"));
    }

    [Fact]
    public void SavedJson_ContainsNoSecretFields()
    {
        var store = new SettingsStore(_root, _root);
        store.Save(new AppSettings());
        var json = File.ReadAllText(store.FilePath);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token\"", json, StringComparison.OrdinalIgnoreCase);
    }
}

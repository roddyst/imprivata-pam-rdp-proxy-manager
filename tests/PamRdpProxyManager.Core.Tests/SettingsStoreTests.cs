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

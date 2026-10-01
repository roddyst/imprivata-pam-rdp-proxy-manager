using System.Security.Cryptography;
using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Core.Tests;

public sealed class SettingsProtectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pam-rdp-tests-" + Guid.NewGuid().ToString("N"));

    public SettingsProtectionTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Stands in for DPAPI: only the holder of the key can create valid signatures.</summary>
    private sealed class HmacProtector(byte[] key) : ISettingsProtector
    {
        public byte[] Sign(byte[] content) => HMACSHA256.HashData(key, content);

        public bool Verify(byte[] content, byte[] signature) => CryptographicOperations.FixedTimeEquals(Sign(content), signature);
    }

    private SettingsStore Store(byte key = 1) => new(_root, _root, new HmacProtector([key, 2, 3]));

    [Fact]
    public void MissingFile_IsNoFile()
    {
        var store = Store();
        store.Load(out _);
        Assert.Equal(SettingsVerification.NoFile, store.Verification);
    }

    [Fact]
    public void SavedByApp_IsVerified()
    {
        Store().Save(new AppSettings());

        var store = Store();
        store.Load(out _);
        Assert.Equal(SettingsVerification.Verified, store.Verification);
    }

    [Fact]
    public void ChangedOutsideApp_IsUnverified()
    {
        var store = Store();
        var settings = new AppSettings();
        settings.Normalize();
        store.Save(settings);

        File.WriteAllText(store.FilePath, File.ReadAllText(store.FilePath).Replace("pam.example.com", "evil.example.com"));

        var loaded = Store().Load(out _);
        Assert.Equal("evil.example.com", loaded.Profiles[0].ProxyHost);
        var check = Store();
        check.Load(out _);
        Assert.Equal(SettingsVerification.Unverified, check.Verification);
    }

    [Fact]
    public void SignatureOfOtherUser_IsUnverified()
    {
        Store(key: 1).Save(new AppSettings());

        var store = Store(key: 9);
        store.Load(out _);
        Assert.Equal(SettingsVerification.Unverified, store.Verification);
    }

    [Fact]
    public void MissingSignature_IsUnverified()
    {
        new SettingsStore(_root, _root).Save(new AppSettings()); // older version without signature

        var store = Store();
        store.Load(out _);
        Assert.Equal(SettingsVerification.Unverified, store.Verification);
    }

    [Fact]
    public void Quarantine_MovesFileAsideAndYieldsDefaults()
    {
        var store = Store();
        var settings = new AppSettings();
        settings.Profiles.Add(new ConnectionProfile { Name = "Evil", ProxyHost = "evil.example.com" });
        new SettingsStore(_root, _root).Save(settings);

        store.Load(out _);
        var backup = store.Quarantine();

        Assert.True(File.Exists(backup));
        Assert.False(File.Exists(store.FilePath));
        Assert.Equal("pam.example.com", store.Load(out _).Profiles.Single().ProxyHost);
    }

    [Fact]
    public void Normalize_MigratesConnectWithoutWarning()
    {
        var settings = new AppSettings { SchemaVersion = 2 };
        settings.Profiles.Add(new ConnectionProfile { Rdp = new RdpOptions { AuthenticationLevel = ServerAuthenticationLevel.ConnectWithoutWarning } });
        settings.Profiles.Add(new ConnectionProfile { Name = "B", Rdp = new RdpOptions { AuthenticationLevel = ServerAuthenticationLevel.DoNotConnect } });

        settings.Normalize();

        Assert.Equal(ServerAuthenticationLevel.Warn, settings.Profiles[0].Rdp.AuthenticationLevel);
        Assert.Equal(ServerAuthenticationLevel.DoNotConnect, settings.Profiles[1].Rdp.AuthenticationLevel);
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void Normalize_ClampsAutoLogout()
    {
        var settings = new AppSettings { AutoLogoutMinutes = 1 };
        settings.Normalize();
        Assert.True(settings.AutoLogoutEnabled);
        Assert.Equal(AppSettings.MinAutoLogoutMinutes, settings.AutoLogoutMinutes);
    }
}

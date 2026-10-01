using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Core.Tests;

public class MfaSessionTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Defaults_EnabledWithEightHours()
    {
        var settings = new AppSettings();
        Assert.True(settings.MfaValidityEnabled);
        Assert.Equal(8, settings.MfaValidityHours);
        Assert.Equal(TimeSpan.FromHours(8), MfaSessionTracker.Validity(settings));
    }

    [Fact]
    public void NoToken_IsNotValid()
    {
        var settings = new AppSettings();
        Assert.False(MfaSessionTracker.IsValid(settings, "jdoe", "pam.example.com", Now));
        Assert.Null(MfaSessionTracker.ExpiresAt(settings, "jdoe", "pam.example.com", Now));
    }

    [Fact]
    public void Start_ValidForConfiguredHours()
    {
        var settings = new AppSettings { MfaValidityHours = 8 };
        MfaSessionTracker.Start(settings, "jdoe", "pam.example.com", Now);

        Assert.True(MfaSessionTracker.IsValid(settings, "jdoe", "pam.example.com", Now.AddHours(7).AddMinutes(59)));
        Assert.False(MfaSessionTracker.IsValid(settings, "jdoe", "pam.example.com", Now.AddHours(8)));
        Assert.Equal(Now.AddHours(8), MfaSessionTracker.ExpiresAt(settings, "jdoe", "pam.example.com", Now));
    }

    [Fact]
    public void Start_IsPerUserAndServer_CaseInsensitive()
    {
        var settings = new AppSettings();
        MfaSessionTracker.Start(settings, "JDoe", "PAM.example.com", Now);

        Assert.True(MfaSessionTracker.IsValid(settings, "jdoe", "pam.example.com", Now));
        Assert.False(MfaSessionTracker.IsValid(settings, "other", "pam.example.com", Now));
        Assert.False(MfaSessionTracker.IsValid(settings, "jdoe", "pam2.example.com", Now));
    }

    [Fact]
    public void Start_AgainRestartsValidity()
    {
        var settings = new AppSettings();
        MfaSessionTracker.Start(settings, "jdoe", "pam.example.com", Now);
        MfaSessionTracker.Start(settings, "jdoe", "pam.example.com", Now.AddHours(6));

        Assert.Single(settings.MfaSessions);
        Assert.True(MfaSessionTracker.IsValid(settings, "jdoe", "pam.example.com", Now.AddHours(13)));
    }

    [Fact]
    public void Disabled_IsNeverValid()
    {
        var settings = new AppSettings();
        MfaSessionTracker.Start(settings, "jdoe", "pam.example.com", Now);
        settings.MfaValidityEnabled = false;

        Assert.False(MfaSessionTracker.IsValid(settings, "jdoe", "pam.example.com", Now));
    }

    [Fact]
    public void Reset_ForgetsToken()
    {
        var settings = new AppSettings();
        MfaSessionTracker.Start(settings, "jdoe", "pam.example.com", Now);

        Assert.True(MfaSessionTracker.Reset(settings, "jdoe", "pam.example.com"));
        Assert.False(MfaSessionTracker.IsValid(settings, "jdoe", "pam.example.com", Now));
    }

    [Fact]
    public void StartInTheFuture_IsNotTrusted()
    {
        var settings = new AppSettings();
        MfaSessionTracker.Start(settings, "jdoe", "pam.example.com", Now.AddHours(2));

        Assert.False(MfaSessionTracker.IsValid(settings, "jdoe", "pam.example.com", Now));
    }

    [Fact]
    public void Prune_RemovesLongExpiredEntries()
    {
        var settings = new AppSettings();
        MfaSessionTracker.Start(settings, "old", "pam.example.com", Now.AddDays(-30));
        MfaSessionTracker.Start(settings, "jdoe", "pam.example.com", Now);

        Assert.Single(settings.MfaSessions);
    }

    [Fact]
    public void Id_ContainsNoUserNameOrServer()
    {
        var id = MfaSessionTracker.IdFor("jdoe", "pam.example.com");
        Assert.DoesNotContain("jdoe", id);
        Assert.DoesNotContain("pam", id);
        Assert.Equal(32, id.Length);
    }

    [Fact]
    public void Normalize_ClampsValidity()
    {
        var settings = new AppSettings { MfaValidityHours = 0 };
        settings.Normalize();
        Assert.Equal(AppSettings.MinMfaValidityHours, settings.MfaValidityHours);

        settings.MfaValidityHours = 10_000;
        settings.Normalize();
        Assert.Equal(AppSettings.MaxMfaValidityHours, settings.MfaValidityHours);
    }
}

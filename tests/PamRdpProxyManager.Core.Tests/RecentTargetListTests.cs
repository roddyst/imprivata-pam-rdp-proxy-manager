using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Core.Tests;

public class RecentTargetListTests
{
    [Fact]
    public void Touch_IsCaseInsensitiveAndUpdatesTimestamp()
    {
        var settings = new AppSettings();
        var t0 = DateTimeOffset.Parse("2026-01-01T10:00:00Z");
        RecentTargetList.Touch(settings, "SRV01", null, t0);
        RecentTargetList.Touch(settings, "srv01", "Lab", t0.AddHours(1));

        var entry = Assert.Single(settings.RecentTargets);
        Assert.Equal(t0.AddHours(1), entry.LastUsed);
        Assert.Equal("Lab", entry.ProfileName);
    }

    [Fact]
    public void Trim_KeepsFavoritesAndNewest()
    {
        var settings = new AppSettings { MaxRecentTargets = 2 };
        var t0 = DateTimeOffset.Parse("2026-01-01T10:00:00Z");
        settings.RecentTargets.Add(new RecentTarget { Host = "fav", IsFavorite = true, LastUsed = t0.AddDays(-100) });

        for (var i = 0; i < 5; i++)
        {
            RecentTargetList.Touch(settings, $"srv{i}", null, t0.AddMinutes(i));
        }

        Assert.Equal(["fav", "srv4", "srv3"], settings.RecentTargets.Select(t => t.Host));
    }
}

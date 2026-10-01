using PamRdpProxyManager.Core.Models;

namespace PamRdpProxyManager.Core.Services;

/// <summary>Maintains the list of recently used targets and favorites.</summary>
public static class RecentTargetList
{
    public static RecentTarget Touch(AppSettings settings, string host, string? profileName, DateTimeOffset? now = null)
    {
        host = host.Trim();
        var entry = settings.RecentTargets.FirstOrDefault(t => string.Equals(t.Host, host, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new RecentTarget { Host = host };
            settings.RecentTargets.Add(entry);
        }

        entry.LastUsed = now ?? DateTimeOffset.Now;
        entry.ProfileName = profileName;
        Trim(settings);
        Sort(settings.RecentTargets);
        return entry;
    }

    /// <summary>Favorites first (alphabetically), then the rest by last use.</summary>
    public static IEnumerable<RecentTarget> Ordered(IEnumerable<RecentTarget> targets) =>
        targets
            .OrderByDescending(t => t.IsFavorite)
            .ThenBy(t => t.IsFavorite ? t.Host : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(t => t.LastUsed);

    public static void Sort(List<RecentTarget> targets)
    {
        var ordered = Ordered(targets).ToList();
        targets.Clear();
        targets.AddRange(ordered);
    }

    /// <summary>Removes the oldest non-favorite entries beyond <see cref="AppSettings.MaxRecentTargets"/>.</summary>
    public static void Trim(AppSettings settings)
    {
        var surplus = settings.RecentTargets
            .Where(t => !t.IsFavorite)
            .OrderByDescending(t => t.LastUsed)
            .Skip(settings.MaxRecentTargets)
            .ToList();

        foreach (var t in surplus)
        {
            settings.RecentTargets.Remove(t);
        }
    }
}

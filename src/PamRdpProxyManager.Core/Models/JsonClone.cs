using System.Text.Json;
using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Core.Models;

internal static class JsonClone
{
    public static T Of<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, SettingsStore.JsonOptions), SettingsStore.JsonOptions)!;
}

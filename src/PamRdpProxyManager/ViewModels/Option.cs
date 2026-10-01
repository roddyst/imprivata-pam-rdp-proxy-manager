namespace PamRdpProxyManager.ViewModels;

/// <summary>A value with a display text, used for combo boxes.</summary>
public sealed record Option<T>(T Value, string Text)
{
    public override string ToString() => Text;
}

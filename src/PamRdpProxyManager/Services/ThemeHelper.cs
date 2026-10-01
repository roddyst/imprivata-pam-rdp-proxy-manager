using System.Windows;
using PamRdpProxyManager.Core.Models;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PamRdpProxyManager.Services;

public static class ThemeHelper
{
    public static void Apply(AppTheme theme)
    {
        switch (theme)
        {
            case AppTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica, true);
                break;
            case AppTheme.System:
                ApplicationThemeManager.ApplySystemTheme(true);
                break;
            default:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica, true);
                break;
        }

        foreach (Window window in Application.Current.Windows)
        {
            ApplicationThemeManager.Apply(window);
        }
    }
}

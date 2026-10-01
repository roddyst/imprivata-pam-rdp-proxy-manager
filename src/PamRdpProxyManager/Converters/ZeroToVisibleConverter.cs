using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PamRdpProxyManager.Converters;

/// <summary>Visible when the bound number is 0 (empty-state placeholders).</summary>
public sealed class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

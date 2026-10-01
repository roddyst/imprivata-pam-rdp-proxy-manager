using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PamRdpProxyManager.Converters;

/// <summary>Binds <c>int</c> properties to <see cref="Wpf.Ui.Controls.NumberBox.Value"/> (<c>double?</c>).</summary>
public sealed class IntToDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? (double)i : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d && !double.IsNaN(d) ? (int)Math.Round(d) : DependencyProperty.UnsetValue;
}

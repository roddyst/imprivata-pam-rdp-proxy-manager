using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.ViewModels;
using Wpf.Ui.Controls;

namespace PamRdpProxyManager.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => TargetBox.Focus();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnRecentDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        // Only react to double clicks on an item (not its buttons, the scrollbar or empty space).
        if (FindItem(source) is { DataContext: RecentTarget target })
        {
            e.Handled = true;

            // Deferred so a token prompt does not open while the mouse button is still captured by the list.
            Dispatcher.BeginInvoke(() => ViewModel.ConnectToCommand.Execute(target));
        }
    }

    /// <summary>Returns the list item that contains <paramref name="source"/>, or <c>null</c> if it is inside a button.</summary>
    private static System.Windows.Controls.ListViewItem? FindItem(DependencyObject source)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            switch (current)
            {
                case ButtonBase:
                    return null;
                case System.Windows.Controls.ListViewItem item:
                    return item;
            }
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current) => current switch
    {
        Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(current),
        FrameworkContentElement content => content.Parent,
        _ => LogicalTreeHelper.GetParent(current),
    };
}

using System.Windows;
using System.Windows.Controls;
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
        if (e.OriginalSource is not DependencyObject source || IsInsideButton(source))
        {
            return;
        }

        // Only react to double clicks on an item, not on the scrollbar or empty space.
        if (ItemsControl.ContainerFromElement(RecentList, source) is System.Windows.Controls.ListViewItem { DataContext: RecentTarget target })
        {
            ViewModel.ConnectToCommand.Execute(target);
        }
    }

    private static bool IsInsideButton(DependencyObject source)
    {
        for (var current = source; current is not null;
             current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (current is ButtonBase)
            {
                return true;
            }

            if (current is System.Windows.Controls.ListViewItem)
            {
                return false;
            }
        }

        return false;
    }
}

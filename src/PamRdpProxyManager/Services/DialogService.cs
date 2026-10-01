using System.Windows;
using PamRdpProxyManager.ViewModels;
using Wpf.Ui.Controls;

namespace PamRdpProxyManager.Services;

public sealed class DialogService : IDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var box = Create(title, message);
        box.PrimaryButtonText = confirmText;
        box.PrimaryButtonAppearance = ControlAppearance.Primary;
        box.CloseButtonText = "Abbrechen";
        return await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var box = Create(title, message);
        box.CloseButtonText = "OK";
        await box.ShowDialogAsync();
    }

    private static Wpf.Ui.Controls.MessageBox Create(string title, string message) => new()
    {
        Title = title,
        Content = new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
        Owner = Application.Current.MainWindow,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };
}

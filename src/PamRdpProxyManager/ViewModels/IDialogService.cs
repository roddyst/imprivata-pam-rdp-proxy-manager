namespace PamRdpProxyManager.ViewModels;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    Task ShowErrorAsync(string title, string message);
}

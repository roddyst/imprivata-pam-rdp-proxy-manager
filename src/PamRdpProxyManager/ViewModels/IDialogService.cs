namespace PamRdpProxyManager.ViewModels;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Asks for the Confirm-ID-Token. Returns <c>null</c> if cancelled, an empty string to connect without token.
    /// <paramref name="reason"/> replaces the default explanation (e.g. "the token has expired").
    /// </summary>
    string? PromptToken(string targetHost, string? reason = null);
}

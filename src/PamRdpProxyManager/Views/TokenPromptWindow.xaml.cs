using System.Windows;
using PamRdpProxyManager.Core.Services;
using Wpf.Ui.Controls;

namespace PamRdpProxyManager.Views;

/// <summary>Asks for the Confirm-ID-Token before connecting to <c>targetHost</c>.</summary>
public partial class TokenPromptWindow : FluentWindow
{
    public TokenPromptWindow(string targetHost, string? reason = null)
    {
        InitializeComponent();
        TargetInfo.Text = reason ?? $"Für die Verbindung zu „{targetHost}“ wurde noch kein Token eingegeben.";
        Loaded += (_, _) => TokenBox.Focus();
    }

    /// <summary>The entered token (empty to connect without one); only set when the dialog returns <c>true</c>.</summary>
    public string? Token { get; private set; }

    private void OnConnectClick(object sender, RoutedEventArgs e)
    {
        var token = TokenBox.Text.Trim();
        var error = token.Length == 0
            ? "Bitte das Token eingeben oder „Ohne Token“ wählen."
            : UsernameBuilder.Validate("user", token, "placeholder");

        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            TokenBox.Focus();
            return;
        }

        Token = token;
        TokenBox.Clear();
        DialogResult = true;
    }

    private void OnWithoutTokenClick(object sender, RoutedEventArgs e)
    {
        Token = string.Empty;
        TokenBox.Clear();
        DialogResult = true;
    }
}

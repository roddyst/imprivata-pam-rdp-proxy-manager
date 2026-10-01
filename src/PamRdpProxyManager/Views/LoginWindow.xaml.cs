using System.Windows;
using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.Core.Services;
using PamRdpProxyManager.Services;
using Wpf.Ui.Controls;

namespace PamRdpProxyManager.Views;

public partial class LoginWindow : FluentWindow
{
    private readonly AppSettings _settings;

    public LoginWindow(AppSettings settings, string? notice = null)
    {
        _settings = settings;
        InitializeComponent();

        if (notice is not null)
        {
            NoticeBar.Message = notice;
            NoticeBar.IsOpen = true;
        }

        var profile = settings.Profiles.FirstOrDefault(p => p.Name == settings.ActiveProfileName) ?? settings.Profiles[0];
        ServerInfo.Text = $"PAM-Server: {profile.ProxyHost}:{profile.Port}  ·  Profil: {profile.Name}";
        RememberBox.IsChecked = settings.RememberUserName;
        UserNameBox.Text = settings.RememberUserName ? settings.LastUserName ?? string.Empty : string.Empty;

        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(UserNameBox.Text))
            {
                UserNameBox.Focus();
            }
            else
            {
                PasswordBox.Focus();
            }
        };
    }

    /// <summary>The entered credentials (only set when the dialog returns <c>true</c>).</summary>
    public UserSession? Session { get; private set; }

    private void OnLoginClick(object sender, RoutedEventArgs e)
    {
        var user = UserNameBox.Text.Trim();
        var token = TokenBox.Text.Trim();

        // Validate with a placeholder target – the real target is chosen in the main window.
        var error = UsernameBuilder.Validate(user, token, "placeholder");
        if (error is null && PasswordBox.SecurePassword.Length == 0)
        {
            error = "Bitte das Passwort eingeben.";
        }

        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        using (var password = PasswordBox.SecurePassword)
        {
            Session = new UserSession(user, password, token);
        }

        PasswordBox.Clear();
        TokenBox.Clear();
        _settings.RememberUserName = RememberBox.IsChecked == true;
        DialogResult = true;
    }
}

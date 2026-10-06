using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.Core.Services;
using PamRdpProxyManager.Services;
using PamRdpProxyManager.ViewModels;
using PamRdpProxyManager.Views;

namespace PamRdpProxyManager;

public partial class App : Application
{
    public const string AppFolderName = "ImprivataPamRdpProxyManager";

    private readonly RdpLauncher _launcher = new();
    private SettingsStore _store = null!;
    private AppSettings _settings = null!;
    private string? _startupWarning;
    private UserSession? _session;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        // Remove the temporary credential even if the app crashes or Windows logs off while a connection is
        // being established – otherwise it would stay readable until the next start.
        AppDomain.CurrentDomain.UnhandledException += (_, _) => _launcher.CleanupNow();
        SessionEnding += (_, _) => _launcher.CleanupNow();

        _store = SettingsStore.ForCurrentProcess(AppFolderName, new DpapiSettingsProtector());
        _settings = _store.Load(out _startupWarning);
        ThemeHelper.Apply(_settings.Theme);

        RdpLauncher.CleanupLeftovers();

        if (_store.Verification == SettingsVerification.Unverified && !await ConfirmUnverifiedSettingsAsync())
        {
            Shutdown();
            return;
        }

        ShowLogin(notice: null);
    }

    /// <summary>
    /// The settings file was not saved by this app for the current Windows user. A manipulated file could send the
    /// password to a foreign server, so the user has to confirm the PAM servers before the settings are used.
    /// </summary>
    private async Task<bool> ConfirmUnverifiedSettingsAsync()
    {
        var text = new StringBuilder()
            .AppendLine("Die Einstellungsdatei wurde nicht von dieser App für Ihr Windows-Konto auf diesem Computer gespeichert.")
            .AppendLine("Das ist normal beim ersten Start nach einem Update oder wenn die Datei von einem anderen PC stammt – sie kann aber auch verändert worden sein.")
            .AppendLine()
            .AppendLine("Ihr Passwort wird an diese PAM-Server übergeben:")
            .Append(SettingsReview.Describe(_settings));

        text.AppendLine().Append("Nur vertrauen, wenn Sie diese Server kennen. „Zurücksetzen“ legt die Datei beiseite und startet mit Standardwerten.");

        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Einstellungen prüfen",
            Content = new System.Windows.Controls.TextBlock { Text = text.ToString(), TextWrapping = TextWrapping.Wrap, MaxWidth = 520 },
            PrimaryButtonText = "Vertrauen",
            SecondaryButtonText = "Zurücksetzen",
            CloseButtonText = "Beenden",
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        switch (await box.ShowDialogAsync())
        {
            case Wpf.Ui.Controls.MessageBoxResult.Primary:
                TrySaveSettings();
                return true;

            case Wpf.Ui.Controls.MessageBoxResult.Secondary:
                try
                {
                    var backup = _store.Quarantine();
                    _settings = _store.Load(out _);
                    _startupWarning = $"Die nicht vertrauenswürdige Einstellungsdatei wurde nach „{backup}“ verschoben. Es werden Standardwerte verwendet.";
                    TrySaveSettings();
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    System.Windows.MessageBox.Show($"Die Einstellungsdatei konnte nicht zurückgesetzt werden: {ex.Message}", "Imprivata PAM RDP Proxy Manager", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

            default:
                return false;
        }
    }

    /// <summary>Saves (and thereby signs) the confirmed settings; a read-only folder only means asking again next time.</summary>
    private void TrySaveSettings()
    {
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _startupWarning = $"Die Einstellungen konnten nicht gespeichert werden: {ex.Message}";
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _launcher.Dispose();
        _session?.Dispose();
        base.OnExit(e);
    }

    private void ShowLogin(string? notice)
    {
        var login = new LoginWindow(_settings, notice);
        if (login.ShowDialog() != true || login.Session is null)
        {
            Shutdown();
            return;
        }

        _session = login.Session;
        var vm = new MainViewModel(_store, _settings, _session, _launcher, new DialogService(), _startupWarning);
        _startupWarning = null;
        vm.TrySave(showSuccess: false); // remembers the user name if enabled

        var main = new MainWindow(vm);
        var loggingOut = false;
        string? logoutNotice = null;
        vm.LogoutRequested += (_, reason) =>
        {
            loggingOut = true;
            logoutNotice = reason;
            main.Close();
        };
        main.Closed += (_, _) =>
        {
            vm.TrySave(showSuccess: false);
            vm.Dispose();
            _launcher.CleanupNow();
            _session?.Dispose();
            _session = null;

            if (loggingOut)
            {
                ShowLogin(logoutNotice);
            }
            else
            {
                Shutdown();
            }
        };

        MainWindow = main;
        main.Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Only the exception type and message are shown – credentials are never part of exception messages we create.
        System.Windows.MessageBox.Show(
            $"Unerwarteter Fehler: {e.Exception.GetType().Name}\n\n{e.Exception.Message}",
            "Imprivata PAM RDP Proxy Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}

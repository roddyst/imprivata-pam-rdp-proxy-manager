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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        _store = SettingsStore.ForCurrentProcess(AppFolderName);
        _settings = _store.Load(out _startupWarning);
        ThemeHelper.Apply(_settings.Theme);

        RdpLauncher.CleanupLeftovers();
        ShowLogin();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _launcher.Dispose();
        _session?.Dispose();
        base.OnExit(e);
    }

    private void ShowLogin()
    {
        var login = new LoginWindow(_settings);
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
        vm.LogoutRequested += (_, _) =>
        {
            loggingOut = true;
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
                ShowLogin();
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

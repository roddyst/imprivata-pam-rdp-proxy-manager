using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PamRdpProxyManager.Core.Models;
using PamRdpProxyManager.Core.Services;
using PamRdpProxyManager.Services;
using Wpf.Ui.Controls;

namespace PamRdpProxyManager.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private readonly UserSession _session;
    private readonly RdpLauncher _launcher;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _mfaTimer;
    private readonly EventHandler _cleanupCompleted;
    private bool _suppressRecentSelection;

    /// <summary>User/PAM server combination and validity seen by the last <see cref="RefreshMfaStatus"/>.</summary>
    private string? _mfaId;
    private bool _mfaWasValid;

    public MainViewModel(SettingsStore store, AppSettings settings, UserSession session, RdpLauncher launcher, IDialogService dialogs, string? startupWarning)
    {
        _store = store;
        _settings = settings;
        _session = session;
        _launcher = launcher;
        _dialogs = dialogs;

        Profiles = new ObservableCollection<ConnectionProfile>(settings.Profiles);
        _selectedProfile = Profiles.FirstOrDefault(p => p.Name == settings.ActiveProfileName) ?? Profiles[0];
        RecentTargets = new ObservableCollection<RecentTarget>(RecentTargetList.Ordered(settings.RecentTargets));
        _token = session.TakeToken();

        if (!store.IsPortable)
        {
            StorageNotice = $"Der Programmordner ist nicht beschreibbar. Einstellungen werden stattdessen unter „{store.Directory}“ gespeichert.";
        }

        if (startupWarning is not null)
        {
            ShowStatus("Hinweis", startupWarning, InfoBarSeverity.Warning);
        }

        _cleanupCompleted = (_, _) => Application.Current?.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(IsCleanupPending)));
        _launcher.CleanupCompleted += _cleanupCompleted;

        RefreshMfaStatus();
        _mfaTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _mfaTimer.Tick += (_, _) => RefreshMfaStatus();
        _mfaTimer.Start();
    }

    public void Dispose()
    {
        _mfaTimer.Stop();
        _launcher.CleanupCompleted -= _cleanupCompleted;
    }

    /// <summary>Raised when the validity of the entered token has expired (reminder to enter a new one).</summary>
    public event EventHandler? MfaExpired;

    /// <summary>Raised when the user wants to log out (returns to the login dialog).</summary>
    public event EventHandler? LogoutRequested;

    public string UserName => _session.UserName;

    public string? StorageNotice { get; }

    public bool HasStorageNotice => StorageNotice is not null;

    public string SettingsFilePath => _store.FilePath;

    public bool IsCleanupPending => _launcher.IsBusy;

    public ObservableCollection<ConnectionProfile> Profiles { get; }

    public ObservableCollection<RecentTarget> RecentTargets { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsernamePreview))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _targetHost = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsernamePreview))]
    private string _token;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    private ConnectionProfile _selectedProfile;

    [ObservableProperty]
    private RecentTarget? _selectedRecentTarget;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isStatusOpen;

    [ObservableProperty]
    private string _statusTitle = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;

    /// <summary><c>true</c> while the token entered for the current user and PAM server is still valid.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TokenPlaceholder))]
    private bool _isMfaValid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMfaStatus))]
    private string _mfaStatus = string.Empty;

    public bool HasMfaStatus => MfaStatus.Length > 0;

    [ObservableProperty]
    private InfoBarSeverity _mfaSeverity = InfoBarSeverity.Informational;

    public bool IsMfaEnabled => _settings.MfaValidityEnabled;

    public string TokenPlaceholder => IsMfaValid ? "Nicht erforderlich – MFA noch gültig" : "Imprivata ID Token";

    /// <summary>Shows how the proxy user name will look (token masked).</summary>
    public string UsernamePreview
    {
        get
        {
            var token = string.IsNullOrWhiteSpace(Token) ? string.Empty : new string('•', Math.Min(Token.Trim().Length, 8));
            var target = string.IsNullOrWhiteSpace(TargetHost) ? "<zielserver>" : TargetHost.Trim();
            return $"{UserName}#{token}#{target}";
        }
    }

    // ----- App settings (bound in the settings tab) -----

    public bool RememberUserName
    {
        get => _settings.RememberUserName;
        set => SetProperty(_settings.RememberUserName, value, _settings, (s, v) => s.RememberUserName = v);
    }

    public bool ClearTokenAfterConnect
    {
        get => _settings.ClearTokenAfterConnect;
        set => SetProperty(_settings.ClearTokenAfterConnect, value, _settings, (s, v) => s.ClearTokenAfterConnect = v);
    }

    public int CleanupDelaySeconds
    {
        get => _settings.CredentialCleanupDelaySeconds;
        set => SetProperty(_settings.CredentialCleanupDelaySeconds, value, _settings, (s, v) => s.CredentialCleanupDelaySeconds = v);
    }

    public bool MfaValidityEnabled
    {
        get => _settings.MfaValidityEnabled;
        set
        {
            if (SetProperty(_settings.MfaValidityEnabled, value, _settings, (s, v) => s.MfaValidityEnabled = v))
            {
                OnPropertyChanged(nameof(IsMfaEnabled));
                RefreshMfaStatus();
            }
        }
    }

    public int MfaValidityHours
    {
        get => _settings.MfaValidityHours;
        set
        {
            if (SetProperty(_settings.MfaValidityHours, value, _settings, (s, v) => s.MfaValidityHours = v))
            {
                RefreshMfaStatus();
            }
        }
    }

    public int MaxRecentTargets
    {
        get => _settings.MaxRecentTargets;
        set => SetProperty(_settings.MaxRecentTargets, value, _settings, (s, v) => s.MaxRecentTargets = v);
    }

    public AppTheme Theme
    {
        get => _settings.Theme;
        set
        {
            if (SetProperty(_settings.Theme, value, _settings, (s, v) => s.Theme = v))
            {
                ThemeHelper.Apply(value);
            }
        }
    }

    // ----- Combo box options -----

    public IReadOnlyList<Option<DisplayMode>> DisplayModes { get; } =
    [
        new(DisplayMode.Fullscreen, "Vollbild"),
        new(DisplayMode.Windowed, "Fenster"),
    ];

    public IReadOnlyList<Option<int>> ColorDepths { get; } =
    [
        new(32, "Höchste Qualität (32 Bit)"),
        new(24, "True Color (24 Bit)"),
        new(16, "High Color (16 Bit)"),
        new(15, "High Color (15 Bit)"),
    ];

    public IReadOnlyList<Option<AudioMode>> AudioModes { get; } =
    [
        new(AudioMode.PlayLocally, "Auf diesem Computer wiedergeben"),
        new(AudioMode.PlayRemotely, "Auf dem Remotecomputer wiedergeben"),
        new(AudioMode.DoNotPlay, "Nicht wiedergeben"),
    ];

    public IReadOnlyList<Option<ServerAuthenticationLevel>> AuthenticationLevels { get; } =
    [
        new(ServerAuthenticationLevel.Warn, "Warnen (empfohlen)"),
        new(ServerAuthenticationLevel.DoNotConnect, "Nicht verbinden"),
        new(ServerAuthenticationLevel.ConnectWithoutWarning, "Ohne Warnung verbinden"),
    ];

    public IReadOnlyList<Option<AppTheme>> Themes { get; } =
    [
        new(AppTheme.Dark, "Dunkel"),
        new(AppTheme.Light, "Hell"),
        new(AppTheme.System, "Systemeinstellung"),
    ];

    // ----- Connect -----

    private bool CanConnect() => !IsConnecting && !string.IsNullOrWhiteSpace(TargetHost);

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        var profile = SelectedProfile;

        if (!ProxyHostParser.TryParse(profile.ProxyHost, out var proxyHost, out var parsedPort, out var hostError))
        {
            ShowStatus("PAM-Server ungültig", $"{hostError} Bitte in den Einstellungen des Profils „{profile.Name}“ prüfen.", InfoBarSeverity.Error);
            return;
        }

        // Normalize "https://pam.example.com/" or "pam.example.com:3390" in the profile.
        profile.ProxyHost = proxyHost;
        if (parsedPort is int p)
        {
            profile.Port = p;
        }

        if (!ProxyHostParser.IsValidPort(profile.Port))
        {
            ShowStatus("Port ungültig", "Der RDP-Port muss zwischen 1 und 65535 liegen.", InfoBarSeverity.Error);
            return;
        }

        var target = TargetHost.Trim();
        var targetError = UsernameBuilder.ValidateTarget(target);
        if (targetError is not null)
        {
            ShowStatus("Eingabe ungültig", targetError, InfoBarSeverity.Error);
            return;
        }

        // While a previously entered token is still valid the PAM server does not need it again (user##rdphost).
        // Afterwards a new token is requested before connecting.
        var token = Token.Trim();
        var mfaEnabled = _settings.MfaValidityEnabled;
        if (mfaEnabled && token.Length == 0 && !MfaSessionTracker.IsValid(_settings, _session.UserName, proxyHost, DateTimeOffset.Now))
        {
            var expired = MfaSessionTracker.ExpiresAt(_settings, _session.UserName, proxyHost, DateTimeOffset.Now) is not null;
            var prompted = _dialogs.PromptToken(
                target,
                expired ? $"Die Gültigkeit des Tokens ({_settings.MfaValidityHours} h) ist abgelaufen. Bitte für die Verbindung zu „{target}“ ein neues Token eingeben." : null);
            if (prompted is null)
            {
                return;
            }

            token = prompted.Trim();
        }

        var validationError = UsernameBuilder.Validate(_session.UserName, token, target);
        if (validationError is not null)
        {
            ShowStatus("Eingabe ungültig", validationError, InfoBarSeverity.Error);
            return;
        }

        var proxyUser = UsernameBuilder.Build(_session.UserName, token, target);

        if (!await ConfirmExistingCredentialAsync(CredentialManager.TargetFor(proxyHost)))
        {
            return;
        }

        IsConnecting = true;
        ShowStatus("Verbinde …", $"{target} über {proxyHost}:{profile.Port}", InfoBarSeverity.Informational);
        try
        {
            var delay = TimeSpan.FromSeconds(Math.Clamp(_settings.CredentialCleanupDelaySeconds, 5, 300));
            await _launcher.LaunchAsync(
                new LaunchRequest(target, proxyHost, profile.Port, profile.Rdp.Clone(), proxyUser, _session.Password, delay),
                onWaiting: () => ShowStatus("Bitte warten …", "Die vorherige Verbindung wird noch aufgebaut. Danach wird automatisch verbunden.", InfoBarSeverity.Informational));
            OnPropertyChanged(nameof(IsCleanupPending));

            if (mfaEnabled && token.Length > 0)
            {
                // The validity starts with this token; it is not needed (and not kept) any more.
                MfaSessionTracker.Start(_settings, _session.UserName, proxyHost, DateTimeOffset.Now);
                Token = string.Empty;
                RefreshMfaStatus();
            }
            else if (_settings.ClearTokenAfterConnect)
            {
                Token = string.Empty;
            }

            RecentTargetList.Touch(_settings, target, profile.Name);
            RefreshRecentTargets();
            TrySave(showSuccess: false);

            ShowStatus(
                "Remotedesktop gestartet",
                $"{target} über {proxyHost}. Die temporären Zugangsdaten werden nach dem Verbindungsaufbau (spätestens nach {delay.TotalSeconds:0} s) entfernt.",
                InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus("Verbindung fehlgeschlagen", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task ConnectToAsync(RecentTarget? target)
    {
        if (target is null)
        {
            return;
        }

        SelectRecent(target);
        if (!ConnectCommand.CanExecute(null))
        {
            return;
        }

        // With the token validity enabled, ConnectAsync asks for a token only when one is needed.
        if (!_settings.MfaValidityEnabled && string.IsNullOrWhiteSpace(Token))
        {
            var token = _dialogs.PromptToken(target.Host);
            if (token is null)
            {
                return;
            }

            Token = token;
        }

        await ConnectCommand.ExecuteAsync(null);
    }

    private async Task<bool> ConfirmExistingCredentialAsync(string credentialTarget)
    {
        CredentialManager.ExistingCredential existing;
        try
        {
            existing = CredentialManager.FindForeign(credentialTarget);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return true; // Could not check – proceed, Write will report real problems.
        }

        return existing switch
        {
            CredentialManager.ExistingCredential.Generic => await _dialogs.ConfirmAsync(
                "Vorhandener Anmeldeeintrag",
                $"Für „{credentialTarget}“ ist bereits ein Eintrag in der Windows-Anmeldeinformationsverwaltung gespeichert.\n\n" +
                "Er wird für diese Verbindung überschrieben und danach entfernt. Fortfahren?",
                "Überschreiben"),
            CredentialManager.ExistingCredential.DomainPassword => await _dialogs.ConfirmAsync(
                "Gespeicherte RDP-Zugangsdaten gefunden",
                $"Für „{credentialTarget}“ sind in Windows bereits RDP-Zugangsdaten gespeichert (z. B. über „Anmeldedaten speichern“ in mstsc).\n\n" +
                "Der Remotedesktop-Client verwendet möglicherweise diese statt des Proxy-Benutzernamens. " +
                "Entfernen Sie den Eintrag ggf. in der Anmeldeinformationsverwaltung.\n\nTrotzdem verbinden?",
                "Trotzdem verbinden"),
            _ => true,
        };
    }

    // ----- Token validity (MFA) -----

    [RelayCommand]
    private void ResetMfa()
    {
        if (CurrentProxyHost() is { } proxyHost && MfaSessionTracker.Reset(_settings, _session.UserName, proxyHost))
        {
            TrySave(showSuccess: false);
        }

        RefreshMfaStatus();
        ShowStatus("Token zurückgesetzt", "Beim nächsten Verbinden wird ein neues Confirm-ID-Token abgefragt.", InfoBarSeverity.Informational);
    }

    private string? CurrentProxyHost() =>
        ProxyHostParser.TryParse(SelectedProfile?.ProxyHost, out var host, out _, out _) ? host : null;

    /// <summary>Updates the validity display and reminds the user once the token has expired.</summary>
    private void RefreshMfaStatus()
    {
        var now = DateTimeOffset.Now;
        var proxyHost = CurrentProxyHost();
        var id = proxyHost is null ? null : MfaSessionTracker.IdFor(_session.UserName, proxyHost);
        var expiresAt = proxyHost is null ? null : MfaSessionTracker.ExpiresAt(_settings, _session.UserName, proxyHost, now);
        var valid = expiresAt is { } end && now < end;

        // Only remind when the validity ran out (not after a reset, disabling the feature or switching the PAM server).
        var expiredJustNow = id == _mfaId && _mfaWasValid && !valid && expiresAt is not null;
        _mfaId = id;
        _mfaWasValid = valid;
        IsMfaValid = valid;

        if (!_settings.MfaValidityEnabled || proxyHost is null)
        {
            MfaStatus = string.Empty;
        }
        else if (expiresAt is not { } expires)
        {
            MfaStatus = "Noch kein Token eingegeben – beim Verbinden wird danach gefragt.";
            MfaSeverity = InfoBarSeverity.Informational;
        }
        else if (valid)
        {
            MfaStatus = $"Token bestätigt – bis {FormatTime(expires, now)} (noch {FormatDuration(expires - now)}) muss es nicht erneut eingegeben werden.";
            MfaSeverity = InfoBarSeverity.Success;
        }
        else
        {
            MfaStatus = $"Token seit {FormatTime(expires, now)} abgelaufen – bitte beim nächsten Verbinden ein neues Token eingeben.";
            MfaSeverity = InfoBarSeverity.Warning;
        }

        if (expiredJustNow)
        {
            ShowStatus(
                "Confirm-ID-Token abgelaufen",
                $"Die Gültigkeit des Tokens ({_settings.MfaValidityHours} h) ist abgelaufen. Bitte beim nächsten Verbinden ein neues Token eingeben.",
                InfoBarSeverity.Warning);
            MfaExpired?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string FormatTime(DateTimeOffset time, DateTimeOffset now) =>
        time.LocalDateTime.Date == now.LocalDateTime.Date ? $"{time.LocalDateTime:HH:mm} Uhr" : $"{time.LocalDateTime:dd.MM. HH:mm} Uhr";

    private static string FormatDuration(TimeSpan span) => span.TotalMinutes < 1
        ? "weniger als 1 min"
        : span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes} min" : $"{span.Minutes} min";

    // ----- Recent targets / favorites -----

    partial void OnSelectedRecentTargetChanged(RecentTarget? value)
    {
        if (value is not null && !_suppressRecentSelection)
        {
            SelectRecent(value);
        }
    }

    private void SelectRecent(RecentTarget target)
    {
        TargetHost = target.Host;
        var profile = Profiles.FirstOrDefault(p => p.Name == target.ProfileName);
        if (profile is not null)
        {
            SelectedProfile = profile;
        }
    }

    [RelayCommand]
    private void ToggleFavorite(RecentTarget? target)
    {
        if (target is null)
        {
            return;
        }

        target.IsFavorite = !target.IsFavorite;
        RecentTargetList.Trim(_settings);
        RefreshRecentTargets();
        TrySave(showSuccess: false);
    }

    [RelayCommand]
    private void AddFavorite()
    {
        var error = UsernameBuilder.ValidateTarget(TargetHost);
        if (error is not null)
        {
            ShowStatus("Eingabe ungültig", error, InfoBarSeverity.Error);
            return;
        }

        var entry = _settings.RecentTargets.FirstOrDefault(t => string.Equals(t.Host, TargetHost.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? RecentTargetList.Touch(_settings, TargetHost, SelectedProfile.Name);
        entry.IsFavorite = true;
        RefreshRecentTargets();
        TrySave(showSuccess: false);
    }

    [RelayCommand]
    private void RemoveRecent(RecentTarget? target)
    {
        if (target is null)
        {
            return;
        }

        _settings.RecentTargets.Remove(target);
        RefreshRecentTargets();
        TrySave(showSuccess: false);
    }

    private void RefreshRecentTargets()
    {
        _suppressRecentSelection = true;
        try
        {
            RecentTargets.Clear();
            foreach (var t in RecentTargetList.Ordered(_settings.RecentTargets))
            {
                RecentTargets.Add(t);
            }
        }
        finally
        {
            _suppressRecentSelection = false;
        }
    }

    // ----- Profiles -----

    partial void OnSelectedProfileChanged(ConnectionProfile value)
    {
        // A ComboBox may push null while its items change (e.g. deleting the selected profile).
        if (value is null)
        {
            SelectedProfile = Profiles[0];
            return;
        }

        _settings.ActiveProfileName = value.Name;
        RefreshMfaStatus();
    }

    [RelayCommand]
    private void NewProfile() => AddProfile(new ConnectionProfile { Name = UniqueProfileName("Neues Profil"), ProxyHost = "pam.example.com" });

    [RelayCommand]
    private void DuplicateProfile() => AddProfile(SelectedProfile.Clone(UniqueProfileName(SelectedProfile.Name + " (Kopie)")));

    private bool CanDeleteProfile() => Profiles.Count > 1;

    [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    private async Task DeleteProfileAsync()
    {
        var profile = SelectedProfile;
        if (!await _dialogs.ConfirmAsync("Profil löschen", $"Profil „{profile.Name}“ wirklich löschen?", "Löschen"))
        {
            return;
        }

        var index = Profiles.IndexOf(profile);
        Profiles.Remove(profile);
        SelectedProfile = Profiles[Math.Clamp(index, 0, Profiles.Count - 1)];
        DeleteProfileCommand.NotifyCanExecuteChanged();
        TrySave(showSuccess: false);
    }

    private void AddProfile(ConnectionProfile profile)
    {
        Profiles.Add(profile);
        SelectedProfile = profile;
        DeleteProfileCommand.NotifyCanExecuteChanged();
    }

    private string UniqueProfileName(string baseName)
    {
        var name = baseName;
        for (var i = 2; Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)); i++)
        {
            name = $"{baseName} {i}";
        }

        return name;
    }

    // ----- Save / misc -----

    [RelayCommand]
    private void Save() => TrySave(showSuccess: true);

    /// <summary>Writes settings.json. Never contains passwords or tokens.</summary>
    public bool TrySave(bool showSuccess)
    {
        foreach (var profile in Profiles)
        {
            profile.Name = profile.Name.Trim();
        }

        if (Profiles.Any(p => p.Name.Length == 0))
        {
            ShowStatus("Speichern nicht möglich", "Jedes Profil benötigt einen Namen.", InfoBarSeverity.Error);
            return false;
        }

        var duplicate = Profiles.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            ShowStatus("Speichern nicht möglich", $"Der Profilname „{duplicate.Key}“ ist mehrfach vergeben.", InfoBarSeverity.Error);
            return false;
        }

        var invalidPort = Profiles.FirstOrDefault(p => !ProxyHostParser.IsValidPort(p.Port));
        if (invalidPort is not null)
        {
            ShowStatus("Speichern nicht möglich", $"Der RDP-Port im Profil „{invalidPort.Name}“ muss zwischen 1 und 65535 liegen.", InfoBarSeverity.Error);
            return false;
        }

        foreach (var profile in Profiles)
        {
            if (ProxyHostParser.TryParse(profile.ProxyHost, out var host, out var port, out _))
            {
                profile.ProxyHost = host;
                profile.Port = port ?? profile.Port;
            }
        }

        _settings.Profiles = [.. Profiles];
        _settings.ActiveProfileName = SelectedProfile.Name;
        _settings.Normalize();
        OnPropertyChanged(nameof(CleanupDelaySeconds));
        OnPropertyChanged(nameof(MaxRecentTargets));
        OnPropertyChanged(nameof(MfaValidityHours));
        _settings.LastUserName = _settings.RememberUserName ? _session.UserName : null;

        try
        {
            _store.Save(_settings);
            if (showSuccess)
            {
                ShowStatus("Gespeichert", $"Einstellungen wurden in „{_store.FilePath}“ gespeichert.", InfoBarSeverity.Success);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus("Speichern fehlgeschlagen", ex.Message, InfoBarSeverity.Error);
            return false;
        }
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        try
        {
            Directory.CreateDirectory(_store.Directory);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { _store.Directory }, UseShellExecute = false })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            ShowStatus("Ordner konnte nicht geöffnet werden", ex.Message, InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    private void Logout() => LogoutRequested?.Invoke(this, EventArgs.Empty);

    private void ShowStatus(string title, string message, InfoBarSeverity severity)
    {
        StatusTitle = title;
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
    }
}

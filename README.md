<p align="center">
  <img src="src/PamRdpProxyManager/Assets/app.png" width="96" alt="Icon">
</p>

<h1 align="center">Imprivata PAM RDP Proxy Manager</h1>

<p align="center">
  Portable Windows-App, um den <b>Imprivata PAM RDP Proxy</b> komfortabel mit dem nativen Remotedesktop-Client (mstsc) zu nutzen.<br>
  <i>Portable Windows app to use the <b>Imprivata PAM RDP proxy</b> comfortably with the native Remote Desktop client (mstsc).</i>
</p>

<p align="center">
  <a href="#deutsch">Deutsch</a> · <a href="#english">English</a>
</p>

> **Hinweis / Note:** Dies ist ein unabhängiges Community-Projekt und steht in keiner Verbindung zu Imprivata, Inc.
> „Imprivata“ ist eine Marke des jeweiligen Inhabers. · *This is an independent community project, not affiliated with
> or endorsed by Imprivata, Inc. "Imprivata" is a trademark of its respective owner.*

---

## Deutsch

### Zweck

Der Imprivata PAM RDP Proxy erwartet als RDP-Benutzernamen eine Zeichenkette im Format

```
user#confirmidtoken#rdphost
```

| Teil             | Bedeutung                                                         |
|------------------|-------------------------------------------------------------------|
| `user`           | Benutzername des Anwenders                                        |
| `confirmidtoken` | Token zur Bestätigung (Imprivata ID) – optional, ohne Token bleibt das Feld leer: `user##rdphost` |
| `rdphost`        | Zielserver, auf den über den Proxy verbunden werden soll          |

Das Passwort wird ganz normal als RDP-Passwort übergeben, die Verbindung geht an den **PAM-Server (RDP-Proxy)** –
nicht direkt an den Zielserver. Diese App nimmt einem das manuelle Zusammenbauen des Benutzernamens und das Eintippen
der Verbindungsdaten ab.

### Screenshots

| Anmeldung | Verbinden | Einstellungen |
|-----------|-----------|---------------|
| *(Screenshot folgt – `docs/screenshots/login.png`)* | *(Screenshot folgt – `docs/screenshots/main.png`)* | *(Screenshot folgt – `docs/screenshots/settings.png`)* |

### Funktionen

- **Startdialog**: Benutzername, Passwort und optional das Confirm-ID-Token
- **Hauptfenster**: Zielserver eingeben, Token pro Verbindung ändern, Vorschau des Proxy-Benutzernamens
- **Favoriten & zuletzt verwendete Ziele** – Klick übernimmt, Doppelklick verbindet
- **Profile** mit PAM-Server (Hostname oder URL), RDP-Port (Standard 3388) und RDP-Optionen:
  Vollbild/Fenster & Auflösung, Multi-Monitor, Farbtiefe, Zwischenablage, Laufwerke, Drucker, Audio/Mikrofon,
  NLA (CredSSP), Verhalten bei fehlgeschlagener Serverauthentifizierung, automatische Wiederverbindung,
  zusätzliche freie `.rdp`-Einstellungen
- Modernes Fluent-Design (WPF-UI) mit Dark Mode (Hell/System wählbar)

### Download & Nutzung (portable EXE)

1. Unter [**Releases**](../../releases) die Datei `PamRdpProxyManager-vX.Y.Z-win-x64.exe` herunterladen.
2. Optional die Prüfsumme kontrollieren (die `.sha256`-Datei liegt daneben):
   ```powershell
   Get-FileHash .\PamRdpProxyManager-vX.Y.Z-win-x64.exe -Algorithm SHA256
   ```
3. EXE in einen beliebigen, **beschreibbaren** Ordner legen (z. B. `C:\Tools\PamRdp\` oder USB-Stick) und starten.
   Keine Installation, keine Adminrechte, kein vorinstalliertes .NET erforderlich.
4. Unter **Einstellungen & Profile** den PAM-Server eintragen (z. B. `pam.example.com`), ggf. den Port anpassen
   (Standard `3388`) und speichern.
5. Anmelden, Zielserver (z. B. `server01.example.com`) eingeben, **Verbinden**.

Voraussetzungen: Windows 10/11 x64 mit dem integrierten Remotedesktop-Client (`mstsc.exe`).

#### SmartScreen-Hinweis

Die EXE ist **nicht signiert**. Windows SmartScreen zeigt beim ersten Start daher evtl. „Der Computer wurde durch
Windows geschützt“. Über **Weitere Informationen → Trotzdem ausführen** lässt sie sich starten. Wer sichergehen will,
vergleicht vorher die SHA256-Prüfsumme mit der im Release angegebenen oder baut die EXE selbst (siehe unten).

### Speicherorte (portabel)

| Was | Wo |
|-----|----|
| Einstellungen, Profile, Favoriten, zuletzt verwendete Ziele | `settings.json` **neben der EXE** |
| Fallback, wenn der EXE-Ordner nicht beschreibbar ist (z. B. `C:\Program Files`) | `%LOCALAPPDATA%\ImprivataPamRdpProxyManager\settings.json` – die App zeigt dann einen Hinweis |
| Temporäre `.rdp`-Datei (ohne Zugangsdaten) | `%TEMP%\PamRdpProxyManager\` – wird nach dem Verbindungsaufbau gelöscht |

Die App legt **keine** Registry-Einträge, Dienste oder Autostart-Einträge an. (Der Windows-Remotedesktop-Client
selbst führt allerdings wie gewohnt eine eigene Liste zuletzt verwendeter Server und vertrauenswürdiger Zertifikate.)

Hinweis zu .NET Single-File: Native WPF-Bibliotheken werden beim Start automatisch in einen Cache unter `%TEMP%\.net\`
entpackt. Das ist Teil des .NET-Single-File-Formats und erfordert keine Rechte.

### Sicherheit

- **Passwort und Token werden nie auf Disk gespeichert** und nicht geloggt. Das Passwort liegt nur als
  `SecureString` im Arbeitsspeicher und wird nie in einen normalen String umgewandelt.
- Die `settings.json` enthält ausschließlich Server, Profile, RDP-Optionen, Ziele und (optional) den Benutzernamen.
- Die temporäre `.rdp`-Datei enthält **weder Passwort noch Token noch Benutzernamen** – nur Serveradresse und Optionen.
- Die Zugangsdaten werden mstsc über einen temporären Eintrag `TERMSRV/<PAM-Server>` in der
  Windows-Anmeldeinformationsverwaltung übergeben (wie `cmdkey /generic:TERMSRV/<host>`), aber
  - direkt per Windows-API (`CredWrite`) – das Passwort taucht so **nicht in einer Prozess-Kommandozeile** auf,
  - nur mit Lebensdauer der **Anmeldesitzung** (`CRED_PERSIST_SESSION`, wird nicht auf Disk geschrieben),
  - und wird **entfernt**, sobald mstsc beendet ist oder die eingestellte Wartezeit (Standard 15 s) abgelaufen ist,
    spätestens beim Abmelden/Beenden der App. Reste nach einem Absturz werden beim nächsten Start bereinigt.
- Verbindungen werden nacheinander aufgebaut, damit ein zweites Ziel nicht den Eintrag eines noch laufenden
  Verbindungsaufbaus überschreibt.

### Fehlerbehebung

- **„Die Anmeldeinformationen haben nicht funktioniert“ / Passwortabfrage erscheint:** Prüfen, ob unter
  *Anmeldeinformationsverwaltung → Windows-Anmeldeinformationen* bereits ein eigener Eintrag `TERMSRV/<PAM-Server>`
  existiert (die App warnt davor). Ggf. die Wartezeit erhöhen.
- **„Ihre Anmeldeinformationen können nicht verwendet werden“ bei NLA:** Manche Gruppenrichtlinien verbieten
  gespeicherte Anmeldedaten mit NTLM-only-Serverauthentifizierung. NLA im Profil deaktivieren, falls der Proxy das
  unterstützt, oder die Richtlinie „Delegierung gespeicherter Anmeldeinformationen zulassen“ prüfen.

### Selbst bauen

Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet test tests/PamRdpProxyManager.Core.Tests
dotnet publish src/PamRdpProxyManager/PamRdpProxyManager.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

Ergebnis: `publish\PamRdpProxyManager.exe`. Das Projekt lässt sich dank `EnableWindowsTargeting` auch unter
Linux/macOS bauen (ausführen nur unter Windows).

**Release erstellen:** Ein Tag `v*` (z. B. `git tag v1.0.0 && git push origin v1.0.0`) startet den Workflow
[`release.yml`](.github/workflows/release.yml), der die portable EXE baut und mit SHA256-Prüfsumme an ein
GitHub-Release hängt.

### Projektstruktur

```
src/PamRdpProxyManager.Core/     Plattformneutrale Logik (Benutzername, .rdp-Erzeugung, Einstellungen) – getestet
src/PamRdpProxyManager/          WPF-App (WPF-UI, MVVM): Views, ViewModels, Windows-Credential-API, mstsc-Start
tests/PamRdpProxyManager.Core.Tests/  xUnit-Tests
tools/make_icon.py               Erzeugt das App-Icon ohne externe Abhängigkeiten
.github/workflows/               CI (Build + Tests) und Release (Tag v*)
```

---

## English

### Purpose

The Imprivata PAM RDP proxy expects the RDP user name in the format

```
user#confirmidtoken#rdphost
```

| Part             | Meaning                                                      |
|------------------|--------------------------------------------------------------|
| `user`           | the user's account name                                      |
| `confirmidtoken` | confirmation token (Imprivata ID) – optional; without a token the segment stays empty: `user##rdphost` |
| `rdphost`        | target server to connect to through the proxy                |

The password is passed as the normal RDP password and the connection goes to the **PAM server (RDP proxy)**, not
directly to the target. This app builds the user name for you and starts the native Remote Desktop client.

### Screenshots

| Login | Connect | Settings |
|-------|---------|----------|
| *(coming soon – `docs/screenshots/login.png`)* | *(coming soon – `docs/screenshots/main.png`)* | *(coming soon – `docs/screenshots/settings.png`)* |

### Features

- **Login dialog**: user name, password and optional confirm ID token
- **Main window**: enter the target server, change the token per connection, preview of the proxy user name
- **Favorites & recently used targets** – click to select, double-click to connect
- **Profiles** with PAM server (host name or URL), RDP port (default 3388) and RDP options: full screen/window &
  resolution, multi-monitor, color depth, clipboard, drives, printers, audio/microphone, NLA (CredSSP), server
  authentication behavior, auto-reconnect, additional raw `.rdp` settings
- Modern Fluent design (WPF-UI) with dark mode (light/system selectable)

### Download & usage (portable EXE)

1. Download `PamRdpProxyManager-vX.Y.Z-win-x64.exe` from [**Releases**](../../releases).
2. Optionally verify the checksum (the `.sha256` file is attached next to it):
   ```powershell
   Get-FileHash .\PamRdpProxyManager-vX.Y.Z-win-x64.exe -Algorithm SHA256
   ```
3. Put the EXE into any **writable** folder (e.g. `C:\Tools\PamRdp\` or a USB stick) and run it.
   No installation, no admin rights, no pre-installed .NET required.
4. In **Einstellungen & Profile** (settings & profiles) enter your PAM server (e.g. `pam.example.com`), adjust the
   port if needed (default `3388`) and save.
5. Log in, enter the target server (e.g. `server01.example.com`) and click **Verbinden** (connect).

Requirements: Windows 10/11 x64 with the built-in Remote Desktop client (`mstsc.exe`). The UI is in German.

#### SmartScreen

The EXE is **not code-signed**, so Windows SmartScreen may show "Windows protected your PC" on first launch. Click
**More info → Run anyway**. If in doubt, compare the SHA256 checksum with the one published in the release, or build
the EXE yourself (see below).

### Storage locations (portable)

| What | Where |
|------|-------|
| Settings, profiles, favorites, recent targets | `settings.json` **next to the EXE** |
| Fallback if the EXE folder is read-only (e.g. `C:\Program Files`) | `%LOCALAPPDATA%\ImprivataPamRdpProxyManager\settings.json` – the app shows a notice |
| Temporary `.rdp` file (no credentials) | `%TEMP%\PamRdpProxyManager\` – deleted after the connection has been established |

The app creates **no** registry entries, services or autostart entries. (The Windows Remote Desktop client itself
keeps its usual list of recent servers and trusted certificates.)

Note on .NET single-file: native WPF libraries are extracted to a cache under `%TEMP%\.net\` on first start. This is
part of the .NET single-file format and needs no special rights.

### Security

- **Passwords and tokens are never written to disk** and never logged. The password is held in memory as a
  `SecureString` only and is never converted into a regular string.
- `settings.json` only contains servers, profiles, RDP options, targets and (optionally) the user name.
- The temporary `.rdp` file contains **no password, no token and no user name** – only the server address and options.
- Credentials are handed to mstsc through a temporary `TERMSRV/<PAM server>` entry in the Windows Credential Manager
  (like `cmdkey /generic:TERMSRV/<host>`), but
  - written via the Windows API (`CredWrite`), so the password **never shows up on a process command line**,
  - with **logon-session** lifetime only (`CRED_PERSIST_SESSION`, never persisted to disk),
  - and **removed** as soon as mstsc exits or the configured delay (default 15 s) has elapsed – at the latest when
    logging out of / closing the app. Leftovers after a crash are cleaned up on the next start.
- Connections are established one after another so a second target cannot overwrite the credential of a connection
  that is still authenticating.

### Troubleshooting

- **Credential prompt appears / "Your credentials did not work":** check *Credential Manager → Windows Credentials*
  for an existing `TERMSRV/<PAM server>` entry of your own (the app warns about it). Increase the cleanup delay if needed.
- **"Your credentials could not be used" with NLA:** some group policies forbid saved credentials with NTLM-only server
  authentication. Disable NLA in the profile if the proxy supports it, or check the policy
  "Allow delegating saved credentials".

### Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet test tests/PamRdpProxyManager.Core.Tests
dotnet publish src/PamRdpProxyManager/PamRdpProxyManager.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

Output: `publish\PamRdpProxyManager.exe`. Thanks to `EnableWindowsTargeting` the project also builds on Linux/macOS
(it only runs on Windows).

**Creating a release:** pushing a tag `v*` (e.g. `git tag v1.0.0 && git push origin v1.0.0`) runs
[`release.yml`](.github/workflows/release.yml), which builds the portable EXE and attaches it with a SHA256 checksum
to a GitHub release.

## License

[MIT](LICENSE)

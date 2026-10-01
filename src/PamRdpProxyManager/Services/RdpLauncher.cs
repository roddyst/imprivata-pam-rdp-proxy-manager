using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using PamRdpProxyManager.Core.Services;

namespace PamRdpProxyManager.Services;

public sealed record LaunchRequest(
    string TargetHost,
    string ProxyHost,
    int Port,
    Core.Models.RdpOptions Options,
    string ProxyUserName,
    SecureString Password,
    TimeSpan CleanupDelay);

/// <summary>
/// Starts mstsc with a temporary .rdp file and a temporary credential and removes both once mstsc
/// had time to establish the connection (or exited).
/// Launches are serialized: the credential for <c>TERMSRV/&lt;proxy&gt;</c> is shared by all targets,
/// so a second connection must not overwrite it while the first one is still authenticating.
/// </summary>
public sealed class RdpLauncher : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _pendingLock = new();
    private readonly List<PendingCleanup> _pending = [];

    public static string TempDirectory { get; } = Path.Combine(Path.GetTempPath(), "PamRdpProxyManager");

    /// <summary>
    /// Time the credential is kept after mstsc has established the TCP connection to the PAM server. mstsc reads
    /// the credential when it starts connecting and uses it for NLA right after the TLS handshake.
    /// </summary>
    private static readonly TimeSpan ConnectedGrace = TimeSpan.FromSeconds(5);

    /// <summary>Longer than the maximum cleanup delay (300 s).</summary>
    private static readonly TimeSpan LeftoverMinAge = TimeSpan.FromMinutes(10);

    public static string MstscPath => Path.Combine(Environment.SystemDirectory, "mstsc.exe");

    /// <summary><c>true</c> while a previous connection still holds the temporary credential.</summary>
    public bool IsBusy => _gate.CurrentCount == 0;

    /// <summary>Raised (on a worker thread) when the temporary credential and file have been removed.</summary>
    public event EventHandler? CleanupCompleted;

    /// <summary>Removes leftovers of a previous run (crash, power loss, ...).</summary>
    public static void CleanupLeftovers()
    {
        try
        {
            // Same age limit as for the files: entries of a concurrently running instance are younger.
            CredentialManager.DeleteLeftovers(LeftoverMinAge);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Debug.WriteLine($"Credential leftover cleanup failed: {ex.GetType().Name}");
        }

        try
        {
            if (!Directory.Exists(TempDirectory))
            {
                return;
            }

            // Files of a concurrently running instance are younger than its cleanup delay.
            var cutoff = DateTime.UtcNow - LeftoverMinAge;
            foreach (var file in Directory.EnumerateFiles(TempDirectory, "*.rdp"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    TryDelete(file);
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(TempDirectory))
            {
                if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                {
                    TryDeleteDirectory(directory);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Temp file leftover cleanup failed: {ex.GetType().Name}");
        }
    }

    /// <summary>
    /// Starts mstsc. Returns once mstsc is running; cleanup continues in the background.
    /// <paramref name="onWaiting"/> is invoked if the launch has to wait for a previous connection.
    /// </summary>
    public async Task LaunchAsync(LaunchRequest request, Action? onWaiting = null, CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            onWaiting?.Invoke();
            await _gate.WaitAsync(cancellationToken);
        }

        PendingCleanup? pending = null;
        try
        {
            var content = RdpFileBuilder.Build(request.ProxyHost, request.Port, request.Options);
            // mstsc shows the .rdp file name in its window title (and the taskbar), so the file is named
            // after the target server. A unique folder per launch keeps parallel connections apart.
            var directory = Path.Combine(TempDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, RdpFileBuilder.FileNameFor(request.TargetHost));
            pending = new PendingCleanup(CredentialManager.TargetFor(request.ProxyHost), file);
            lock (_pendingLock)
            {
                _pending.Add(pending);
            }

            // mstsc itself writes .rdp files as UTF-16 LE with BOM.
            await File.WriteAllTextAsync(file, content, Encoding.Unicode, cancellationToken);
            CredentialManager.Write(pending.CredentialTarget, request.ProxyUserName, request.Password);

            var process = Process.Start(new ProcessStartInfo(MstscPath)
            {
                ArgumentList = { file },
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException("mstsc.exe konnte nicht gestartet werden.");

            var owned = pending;
            pending = null; // ownership passes to the background cleanup
            _ = Task.Run(async () =>
            {
                try
                {
                    await WaitUntilCredentialUsedAsync(process, request.Port, request.CleanupDelay);
                }
                finally
                {
                    process.Dispose();
                    Cleanup(owned);
                    _gate.Release();
                    CleanupCompleted?.Invoke(this, EventArgs.Empty);
                }
            }, CancellationToken.None);
        }
        catch
        {
            if (pending is not null)
            {
                Cleanup(pending);
            }

            _gate.Release();
            throw;
        }
    }

    /// <summary>Immediately removes all temporary credentials and files (on logout/exit).</summary>
    public void CleanupNow()
    {
        PendingCleanup[] items;
        lock (_pendingLock)
        {
            items = [.. _pending];
        }

        foreach (var item in items)
        {
            Cleanup(item);
        }
    }

    public void Dispose() => CleanupNow();

    /// <summary>
    /// Waits until mstsc has used the credential so it can be removed as early as possible: any program running
    /// under the same Windows account could read it while it exists. Returns when mstsc exited, a few seconds after
    /// it connected to the PAM server, or at the latest after <paramref name="maxDelay"/>.
    /// </summary>
    private static async Task WaitUntilCredentialUsedAsync(Process process, int proxyPort, TimeSpan maxDelay)
    {
        using var cts = new CancellationTokenSource(maxDelay);
        try
        {
            while (true)
            {
                if (process.HasExited)
                {
                    return;
                }

                var connected = SystemState.HasEstablishedTcpConnection(process.Id, proxyPort);
                if (connected is null)
                {
                    // Connection table not available – fall back to the fixed delay.
                    await process.WaitForExitAsync(cts.Token);
                    return;
                }

                if (connected == true)
                {
                    var grace = ConnectedGrace < maxDelay ? ConnectedGrace : maxDelay;
                    using var graceCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                    graceCts.CancelAfter(grace);
                    try
                    {
                        await process.WaitForExitAsync(graceCts.Token);
                    }
                    catch (OperationCanceledException) when (!cts.IsCancellationRequested)
                    {
                        // Grace period elapsed.
                    }

                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Maximum delay elapsed – mstsc is still running and has used the credential by now.
        }
        catch (InvalidOperationException)
        {
            // Process information no longer available.
        }
    }

    private void Cleanup(PendingCleanup item)
    {
        lock (_pendingLock)
        {
            if (!_pending.Remove(item))
            {
                return; // already cleaned up
            }
        }

        try
        {
            CredentialManager.DeleteOwn(item.CredentialTarget);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Debug.WriteLine($"Credential cleanup failed: {ex.GetType().Name}");
        }

        TryDelete(item.RdpFile);
        if (Path.GetDirectoryName(item.RdpFile) is { } directory && !PathEquals(directory, TempDirectory))
        {
            TryDeleteDirectory(directory);
        }
    }

    private static bool PathEquals(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not delete temp folder: {ex.GetType().Name}");
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not delete temp file: {ex.GetType().Name}");
        }
    }

    private sealed record PendingCleanup(string CredentialTarget, string RdpFile);
}

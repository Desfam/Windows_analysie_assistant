using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.Json;
using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

public interface ICapabilityDiscoveryService
{
    /// <summary>
    /// Gibt die zwischengespeicherten Systemfähigkeiten zurück.
    /// Beim ersten Aufruf werden sie ermittelt.
    /// </summary>
    Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>Startet die Erkennung im Hintergrund, ohne auf das Ergebnis zu warten.</summary>
    Task WarmUpAsync();

    /// <summary>Verwirft den Cache, sodass beim nächsten Aufruf neu ermittelt wird.</summary>
    void Invalidate();
}

/// <summary>
/// Ermittelt einmalig beim ersten Aufruf, welche Diagnosewerkzeuge auf dem System verfügbar sind.
/// Alle Abfragen sind ausschließlich lesend.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CapabilityDiscoveryService : ICapabilityDiscoveryService
{
    private static readonly string[] KnownBinaries =
    {
        "ipconfig.exe",
        "ping.exe",
        "nslookup.exe",
        "tracert.exe",
        "netstat.exe",
        "netsh.exe",
        "net.exe",
        "sc.exe",
        "wpr.exe",
        "wevtutil.exe",
        "systeminfo.exe",
        "tasklist.exe",
        "curl.exe",
        "chkdsk.exe",
        "diskpart.exe",
        "fsutil.exe",
        "bcdedit.exe",
        "nltest.exe",
        "dcdiag.exe",
        "winver.exe"
    };

    private static readonly string[] SystemDirs =
    {
        Environment.SystemDirectory,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysNative"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0")
    };

    private readonly ISafeProcessRunner _processRunner;
    private readonly ILogger<CapabilityDiscoveryService> _logger;

    private SystemCapabilities? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CapabilityDiscoveryService(ISafeProcessRunner processRunner, ILogger<CapabilityDiscoveryService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            // Return a minimal fast-path if discovery takes longer than 5 seconds (e.g. first LLM request).
            // The background warm-up will populate the cache shortly after.
            using var fastTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, fastTimeout.Token);
            try
            {
                _cached = await DiscoverAsync(linked.Token);
            }
            catch (OperationCanceledException) when (fastTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Capability discovery hat 5s überschritten – verwende minimale Fähigkeiten für diesen Aufruf.");
                // Return minimal capabilities so the first request isn't blocked;
                // a background warmup task will populate the real cache shortly.
                _cached = BuildMinimalCapabilities();
                _ = Task.Run(() => WarmUpAsync(), CancellationToken.None);
            }
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Triggered at app startup to warm the cache in the background before the first user request.</summary>
    public async Task WarmUpAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var caps = await DiscoverAsync(cts.Token);
            await _lock.WaitAsync(CancellationToken.None);
            try { _cached = caps; }
            finally { _lock.Release(); }
            _logger.LogInformation("Capability warm-up abgeschlossen.");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Capability warm-up fehlgeschlagen.");
        }
    }

    private static SystemCapabilities BuildMinimalCapabilities() => new()
    {
        WindowsVersion = Environment.OSVersion.Version.ToString(),
        Build = Environment.OSVersion.Version.Build.ToString(),
        Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
        IsAdministrator = IsCurrentUserAdmin(),
        WindowsPowerShellVersion = "5.1",
        DetectedAt = DateTimeOffset.Now
    };

    public void Invalidate()
    {
        _lock.Wait();
        try
        {
            _cached = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<SystemCapabilities> DiscoverAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Capability discovery gestartet.");

        var windowsVersion = Environment.OSVersion.Version.ToString();
        var build = GetBuildNumber();
        var edition = GetEdition();
        var architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        var isAdmin = IsCurrentUserAdmin();
        var wpsVersion = GetWindowsPowerShellVersion();

        var (ps7Installed, ps7Version) = await DetectPowerShell7Async(cancellationToken);
        var modules = await DiscoverModulesAsync(cancellationToken);
        var commands = await DiscoverCommandsAsync(cancellationToken);
        var binaries = DiscoverBinaries();

        _logger.LogInformation(
            "Capability discovery abgeschlossen: Build={Build} IsAdmin={IsAdmin} PS7={PS7} Modules={ModuleCount} Binaries={BinaryCount}",
            build, isAdmin, ps7Installed, modules.Count, binaries.Count);

        return new SystemCapabilities
        {
            WindowsVersion = windowsVersion,
            Build = build,
            Edition = edition,
            Architecture = architecture,
            IsAdministrator = isAdmin,
            WindowsPowerShellVersion = wpsVersion,
            PowerShell7Installed = ps7Installed,
            PowerShell7Version = ps7Version,
            AvailableModules = modules,
            AvailableCommands = commands,
            AvailableBinaries = binaries,
            DetectedAt = DateTimeOffset.Now
        };
    }

    private static string GetBuildNumber()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("CurrentBuildNumber")?.ToString() ?? Environment.OSVersion.Version.Build.ToString();
        }
        catch
        {
            return Environment.OSVersion.Version.Build.ToString();
        }
    }

    private static string GetEdition()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("EditionID")?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static bool IsCurrentUserAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string GetWindowsPowerShellVersion()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\PowerShell\3\PowerShellEngine");
            return key?.GetValue("PowerShellVersion")?.ToString() ?? "5.1";
        }
        catch
        {
            return "5.1";
        }
    }

    private async Task<(bool installed, string? version)> DetectPowerShell7Async(CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            @"C:\Program Files\PowerShell\7\pwsh.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe")
        };

        foreach (var candidate in candidates.Where(File.Exists))
        {
            try
            {
                var request = new SafeProcessRequest(candidate, new[] { "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()" }, TimeSpan.FromSeconds(10));
                var result = await _processRunner.RunAsync(request, cancellationToken);
                if (!result.TimedOut && result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    return (true, result.StandardOutput.Trim());
                }
                return (true, null);
            }
            catch
            {
                return (true, null);
            }
        }

        return (false, null);
    }

    private async Task<IReadOnlyDictionary<string, string>> DiscoverModulesAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var powershell = GetWindowsPowerShellPath();
            if (powershell is null) return result;

            // Only check modules we actually use in actions — Get-Module -ListAvailable is too slow (20-30s).
            var relevantModules = new[]
            {
                "DnsClient", "NetTCPIP", "NetAdapter", "Storage", "ActiveDirectory",
                "BitsTransfer", "PSReadLine", "Hyper-V", "WindowsUpdate", "DISM"
            };

            var checks = string.Join("; ", relevantModules.Select(m =>
                $"if (Get-Module -Name '{m}' -ListAvailable -ErrorAction SilentlyContinue) " +
                $"{{ Write-Output '{m}' }}"));

            var script = $"[Console]::OutputEncoding=[Text.UTF8Encoding]::new(); {checks}";
            var request = new SafeProcessRequest(powershell,
                new[] { "-NoProfile", "-NonInteractive", "-Command", script },
                TimeSpan.FromSeconds(15));   // Short timeout — only 10 targeted modules
            var execution = await _processRunner.RunAsync(request, cancellationToken);

            if (!execution.TimedOut && execution.ExitCode == 0 && !string.IsNullOrWhiteSpace(execution.StandardOutput))
            {
                foreach (var line in execution.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = line.Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        result.TryAdd(name, string.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Modul-Erkennung fehlgeschlagen.");
        }

        return result;
    }

    private async Task<IReadOnlyCollection<string>> DiscoverCommandsAsync(CancellationToken cancellationToken)
    {
        // Wir prüfen nur eine ausgewählte Liste bekannter Diagnose-Cmdlets,
        // keine vollständige Get-Command-Enumeration (zu langsam).
        var relevantCommands = new[]
        {
            "Get-WinEvent", "Get-EventLog",
            "Get-NetIPConfiguration", "Get-NetIPAddress", "Get-NetAdapter", "Get-NetRoute",
            "Resolve-DnsName", "Test-NetConnection",
            "Get-Disk", "Get-Volume", "Get-PhysicalDisk", "Get-StorageReliabilityCounter",
            "Get-Service", "Get-Process",
            "Get-ComputerInfo",
            "Get-HotFix",
            "Get-WindowsUpdateLog",
            "Get-ADDomainController", "Test-ComputerSecureChannel",
            "Get-AppxPackage"
        };

        var available = new List<string>();

        try
        {
            var powershell = GetWindowsPowerShellPath();
            if (powershell is null)
            {
                return available;
            }

            var checkScript = string.Join("; ",
                relevantCommands.Select(cmd =>
                    $"if (Get-Command '{cmd}' -ErrorAction SilentlyContinue) {{ Write-Output '{cmd}' }}"));

            var script = $"[Console]::OutputEncoding=[Text.UTF8Encoding]::new(); {checkScript}";
            var request = new SafeProcessRequest(powershell, new[] { "-NoProfile", "-NonInteractive", "-Command", script }, TimeSpan.FromSeconds(30));
            var execution = await _processRunner.RunAsync(request, cancellationToken);

            if (!execution.TimedOut && execution.ExitCode == 0)
            {
                available.AddRange(
                    execution.StandardOutput
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => !string.IsNullOrWhiteSpace(l)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cmdlet-Erkennung fehlgeschlagen.");
        }

        return available;
    }

    private static IReadOnlyDictionary<string, string> DiscoverBinaries()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var searchDirs = SystemDirs
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var binary in KnownBinaries)
        {
            foreach (var dir in searchDirs)
            {
                var full = Path.Combine(dir, binary);
                if (File.Exists(full))
                {
                    result.TryAdd(binary, full);
                    break;
                }
            }
        }

        return result;
    }

    private static string? GetWindowsPowerShellPath()
    {
        var path = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(path) ? path : null;
    }
}

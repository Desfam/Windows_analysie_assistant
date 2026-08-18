using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text.Json;
using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

public interface IDiagnosticActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(string actionId, object validatedParameters, CancellationToken cancellationToken);
}

/// <summary>Führt ausschließlich im Katalog freigegebene, lesende Diagnosen aus.</summary>
public sealed class DiagnosticActionExecutor : IDiagnosticActionExecutor
{
    private const int MessageMaxLength = 500;
    private readonly IEventLogService _eventLogService;
    private readonly ISafeProcessRunner _processRunner;
    private readonly ILogger<DiagnosticActionExecutor> _logger;

    public DiagnosticActionExecutor(IEventLogService eventLogService, ISafeProcessRunner processRunner, ILogger<DiagnosticActionExecutor> logger)
    {
        _eventLogService = eventLogService;
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<ActionExecutionResult> ExecuteAsync(string actionId, object validatedParameters, CancellationToken cancellationToken)
    {
        var result = actionId switch
        {
            "events.query" => await ExecuteEventsQueryAsync((EventsQueryParameters)validatedParameters, cancellationToken),

            "events.system.recent" => await ExecuteRecentEventsAsync(actionId,
                new[] { "System" }, (SinceHoursParameters)validatedParameters, cancellationToken),
            "events.application.recent" => await ExecuteRecentEventsAsync(actionId,
                new[] { "Application" }, (SinceHoursParameters)validatedParameters, cancellationToken),
            "events.kernel_power" => await ExecuteKernelPowerEventsAsync(cancellationToken),
            "events.whea" => await ExecuteWheaEventsAsync(cancellationToken),
            "storage.events.errors" => await ExecuteStorageEventsAsync((SinceHoursParameters)validatedParameters, cancellationToken),

            "system.info" => ExecuteSystemInfo(),
            "system.uptime" => ExecuteSystemUptime(),
            "system.windows_version" => ExecuteWindowsVersion(),
            "system.pending_reboot" => ExecutePendingReboot(),

            "storage.summary" or "system.storage.summary" => ExecuteStorageSummary(),
            "storage.disks.list" => await ExecuteDiskListAsync(cancellationToken),
            "storage.volumes.list" => ExecuteVolumeList(),
            "storage.health.basic" => await ExecuteStorageHealthAsync(cancellationToken),

            "network.microsoftEndpoints" or "network.basicConnectivity" => await ExecuteConnectivityAsync(cancellationToken),
            "network.adapters.list" => await ExecuteNetworkAdaptersAsync(cancellationToken),
            "network.configuration" => await ExecuteNetworkConfigurationAsync(cancellationToken),
            "network.gateway.test" => await ExecuteGatewayTestAsync(cancellationToken),
            "network.dns.resolve" => await ExecuteDnsResolveAsync((DnsResolveParameters)validatedParameters, cancellationToken),
            "network.port.test" => await ExecutePortTestAsync((PortTestParameters)validatedParameters, cancellationToken),

            "process.list" => ExecuteProcessList((ProcessListParameters)validatedParameters, sortBy: "name"),
            "process.cpu_top" => ExecuteProcessList((ProcessListParameters)validatedParameters, sortBy: "cpu"),
            "process.memory_top" => ExecuteProcessList((ProcessListParameters)validatedParameters, sortBy: "memory"),

            "service.list" => ExecuteServiceList(),
            "service.status" => ExecuteServiceStatus((ServiceStatusParameters)validatedParameters),

            "domain.status" => ExecuteDomainStatus(),
            "domain.dc_discovery" => await ExecuteDcDiscoveryAsync(cancellationToken),
            "domain.secure_channel.test" => await ExecuteSecureChannelTestAsync(cancellationToken),

            "winget.status" => await ExecuteWingetStatusAsync(cancellationToken),
            "winget.sources.list" => await ExecuteWingetSourcesAsync(cancellationToken),
            "appinstaller.status" => await ExecuteAppInstallerStatusAsync(cancellationToken),
            "windowsupdate.status" => ExecuteWindowsUpdateStatus(),

            _ => Failed(actionId, "Diese Diagnoseaktion ist noch nicht implementiert.", errorCode: "ACTION_NOT_FOUND")
        };

        Audit(result);
        return result;
    }

    private async Task<ActionExecutionResult> ExecuteWingetStatusAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var wingetPath = ResolveWingetPath();
        if (wingetPath is null)
        {
            const string error = "winget.exe wurde für den aktuellen Benutzer nicht gefunden.";
            return new ActionExecutionResult
            {
                ActionId = "winget.status", Success = false, StartedAt = startedAt, CompletedAt = DateTimeOffset.Now, Error = error,
                Data = new WingetStatusActionResult { Available = false, Callable = false, UserContext = CurrentUserContext() },
                Execution = UnavailableExecution("winget.exe", new[] { "--version" }, startedAt, error)
            };
        }

        var execution = await _processRunner.RunAsync(new SafeProcessRequest(wingetPath, new[] { "--version" }, TimeSpan.FromSeconds(15)), cancellationToken);
        var version = execution.StandardOutput.Trim();
        var callable = !execution.TimedOut && execution.StartError is null;
        var success = callable && execution.ExitCode == 0 && !string.IsNullOrWhiteSpace(version);
        return new ActionExecutionResult
        {
            ActionId = "winget.status", Success = success, StartedAt = execution.StartedAt, CompletedAt = execution.CompletedAt,
            Error = success ? null : DescribeProcessFailure(execution, "Die Winget-Version konnte nicht gelesen werden."), Execution = execution,
            Data = new WingetStatusActionResult
            {
                Available = true, Path = wingetPath, Version = string.IsNullOrWhiteSpace(version) ? null : version,
                Callable = callable, UserContext = CurrentUserContext()
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteWingetSourcesAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var wingetPath = ResolveWingetPath();
        if (wingetPath is null)
        {
            const string error = "winget.exe wurde für den aktuellen Benutzer nicht gefunden.";
            return new ActionExecutionResult
            {
                ActionId = "winget.sources.list", Success = false, StartedAt = startedAt, CompletedAt = DateTimeOffset.Now, Error = error,
                Data = new WingetSourcesActionResult { Available = false, ProcessSucceeded = false },
                Execution = UnavailableExecution("winget.exe", new[] { "source", "list" }, startedAt, error)
            };
        }

        var execution = await _processRunner.RunAsync(new SafeProcessRequest(wingetPath, new[] { "source", "list" }, TimeSpan.FromSeconds(20)), cancellationToken);
        var processSucceeded = !execution.TimedOut && execution.StartError is null && execution.ExitCode == 0;
        var sources = processSucceeded ? ParseSources(execution.StandardOutput) : new List<WingetSourceItem>();
        return new ActionExecutionResult
        {
            ActionId = "winget.sources.list", Success = processSucceeded, StartedAt = execution.StartedAt, CompletedAt = execution.CompletedAt,
            Error = processSucceeded ? null : DescribeProcessFailure(execution, "Die Winget-Quellen konnten nicht gelesen werden."), Execution = execution,
            Data = new WingetSourcesActionResult
            {
                Available = true, Path = wingetPath, ProcessSucceeded = processSucceeded, Parsed = sources.Count > 0,
                Sources = sources, RawOutput = execution.StandardOutput
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteAppInstallerStatusAsync(CancellationToken cancellationToken)
    {
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        const string script = "[Console]::OutputEncoding=[Text.UTF8Encoding]::new(); Get-AppxPackage -Name Microsoft.DesktopAppInstaller | Select-Object Name,Version,InstallLocation,PackageFullName,Status | ConvertTo-Json -Compress";
        var execution = await _processRunner.RunAsync(new SafeProcessRequest(powershell, new[] { "-NoProfile", "-NonInteractive", "-Command", script }, TimeSpan.FromSeconds(15)), cancellationToken);
        var processSucceeded = !execution.TimedOut && execution.StartError is null && execution.ExitCode == 0;
        object? package = null;
        var parsed = false;
        if (processSucceeded && !string.IsNullOrWhiteSpace(execution.StandardOutput))
        {
            try
            {
                using var document = JsonDocument.Parse(execution.StandardOutput);
                package = document.RootElement.Clone();
                parsed = true;
            }
            catch (JsonException) { }
        }

        return new ActionExecutionResult
        {
            ActionId = "appinstaller.status", Success = processSucceeded, StartedAt = execution.StartedAt, CompletedAt = execution.CompletedAt,
            Execution = execution, Error = processSucceeded ? null : DescribeProcessFailure(execution, "Der App-Installer-Status konnte nicht gelesen werden."),
            Data = new DiagnosticStatusActionResult
            {
                Summary = processSucceeded ? "App-Installer-Status wurde lokal gelesen." : "App-Installer-Status konnte nicht gelesen werden.",
                Values = new()
                {
                    ["processSucceeded"] = processSucceeded, ["parsed"] = parsed, ["installed"] = parsed,
                    ["package"] = package, ["rawOutput"] = execution.StandardOutput, ["userContext"] = CurrentUserContext()
                }
            }
        };
    }

    private ActionExecutionResult ExecuteWindowsUpdateStatus()
    {
        var startedAt = DateTimeOffset.Now;
        var services = new Dictionary<string, object?>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, State, StartMode FROM Win32_Service WHERE Name='wuauserv' OR Name='BITS' OR Name='AppXSvc' OR Name='ClipSVC'");
            foreach (ManagementObject service in searcher.Get())
            {
                services[service["Name"]?.ToString() ?? "unknown"] = new { state = service["State"]?.ToString(), startMode = service["StartMode"]?.ToString() };
            }
        }
        catch (ManagementException ex)
        {
            return Failed("windowsupdate.status", "Die Dienstzustände konnten nicht gelesen werden: " + ex.Message, startedAt);
        }
        var rebootPending = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS", "pending.xml"));
        return new ActionExecutionResult
        {
            ActionId = "windowsupdate.status", Success = true, StartedAt = startedAt, CompletedAt = DateTimeOffset.Now,
            Data = new DiagnosticStatusActionResult { Summary = "Windows-Update-Dienstzustände wurden lokal gelesen.", Values = new() { ["services"] = services, ["rebootPending"] = rebootPending, ["userContext"] = CurrentUserContext() } }
        };
    }

    private ActionExecutionResult ExecuteStorageSummary()
    {
        var startedAt = DateTimeOffset.Now;
        var drives = DriveInfo.GetDrives().Where(drive => drive.IsReady).Select(drive => new
        {
            drive.Name, drive.DriveType, drive.TotalSize, drive.AvailableFreeSpace,
            freePercent = drive.TotalSize == 0 ? 0 : Math.Round(drive.AvailableFreeSpace * 100d / drive.TotalSize, 1)
        }).ToList();
        return new ActionExecutionResult
        {
            ActionId = "storage.summary", Success = true, StartedAt = startedAt, CompletedAt = DateTimeOffset.Now,
            Data = new DiagnosticStatusActionResult { Summary = "Datenträgerinformationen wurden lokal gelesen.", Values = new() { ["drives"] = drives } }
        };
    }

    private static async Task<ActionExecutionResult> ExecuteConnectivityAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var hosts = new[] { "cdn.winget.microsoft.com", "storeedgefd.dsx.mp.microsoft.com" };
        var results = new List<object>();
        foreach (var host in hosts)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                results.Add(new { host, resolved = addresses.Length > 0, addresses = addresses.Select(address => address.ToString()).ToArray() });
            }
            catch (SocketException)
            {
                results.Add(new { host, resolved = false, addresses = Array.Empty<string>() });
            }
        }
        var resolved = results.All(result => (bool)result.GetType().GetProperty("resolved")!.GetValue(result)!);
        return new ActionExecutionResult
        {
            ActionId = "network.microsoftEndpoints", Success = resolved, StartedAt = startedAt, CompletedAt = DateTimeOffset.Now,
            Error = resolved ? null : "Mindestens ein fest definierter Microsoft-Endpunkt konnte nicht per DNS aufgelöst werden.",
            Data = new DiagnosticStatusActionResult { Summary = "Die DNS-Auflösung fester Microsoft-Endpunkte wurde lokal geprüft.", Values = new() { ["endpoints"] = results } }
        };
    }

    private async Task<ActionExecutionResult> ExecuteEventsQueryAsync(EventsQueryParameters parameters, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var query = new EventQuery { Levels = parameters.Levels.Select(MapLevel).Distinct().ToList(), Hours = parameters.SinceHours, Logs = parameters.LogNames };
        EventsResponse response;
        try { response = await _eventLogService.GetEventsAsync(query, cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "events.query fehlgeschlagen.");
            return Failed("events.query", "Die Ereignisabfrage ist fehlgeschlagen.", startedAt);
        }
        var events = response.Events.Where(e => !IsInternalDiagnosticNoise(e))
            .Where(e => parameters.Providers.Count == 0 || parameters.Providers.Any(p => e.ProviderName?.Contains(p, StringComparison.OrdinalIgnoreCase) == true))
            .OrderByDescending(e => e.LastSeen).Take(parameters.MaximumResults)
            .Select(e => new EventsQueryResultEvent { EventId = e.EventId, Provider = e.ProviderName, Level = e.Severity.ToString(), Timestamp = e.LastSeen, Message = Truncate(e.OriginalMessage ?? e.Summary ?? string.Empty) }).ToList();
        return new ActionExecutionResult { ActionId = "events.query", Success = true, StartedAt = startedAt, CompletedAt = DateTimeOffset.Now, Data = new EventsQueryActionResult { Query = parameters, Events = events, Summary = new EventsQuerySummary { Total = events.Count } } };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // System-Aktionen
    // ──────────────────────────────────────────────────────────────────────────

    private ActionExecutionResult ExecuteSystemInfo()
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Manufacturer, Model, SystemType, UserName, TotalPhysicalMemory FROM Win32_ComputerSystem");
            using var os = new ManagementObjectSearcher("SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem");
            var csObj = cs.Get().OfType<ManagementObject>().FirstOrDefault();
            var osObj = os.Get().OfType<ManagementObject>().FirstOrDefault();
            return new ActionExecutionResult
            {
                ActionId = "system.info",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = "Systeminformationen wurden lokal gelesen.",
                    Values = new()
                    {
                        ["machineName"] = Environment.MachineName,
                        ["manufacturer"] = csObj?["Manufacturer"]?.ToString(),
                        ["model"] = csObj?["Model"]?.ToString(),
                        ["systemType"] = csObj?["SystemType"]?.ToString(),
                        ["totalPhysicalMemoryBytes"] = csObj?["TotalPhysicalMemory"],
                        ["osCaption"] = osObj?["Caption"]?.ToString(),
                        ["osVersion"] = osObj?["Version"]?.ToString(),
                        ["osArchitecture"] = osObj?["OSArchitecture"]?.ToString(),
                        ["currentUser"] = CurrentUserContext(),
                        ["processorCount"] = Environment.ProcessorCount
                    }
                }
            };
        }
        catch (ManagementException ex)
        {
            return Failed("system.info", "Systeminformationen konnten nicht gelesen werden: " + ex.Message, startedAt);
        }
    }

    private ActionExecutionResult ExecuteSystemUptime()
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            using var os = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            var osObj = os.Get().OfType<ManagementObject>().FirstOrDefault();
            var bootRaw = osObj?["LastBootUpTime"]?.ToString();
            DateTimeOffset? boot = null;
            string? uptime = null;
            if (bootRaw is not null)
            {
                var bootTime = ManagementDateTimeConverter.ToDateTime(bootRaw);
                boot = bootTime;
                var span = DateTime.Now - bootTime;
                uptime = span.TotalDays >= 1
                    ? $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}min"
                    : $"{span.Hours}h {span.Minutes}min";
            }
            return new ActionExecutionResult
            {
                ActionId = "system.uptime",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = "Systemlaufzeit wurde lokal gelesen.",
                    Values = new() { ["lastBootTime"] = boot, ["uptime"] = uptime }
                }
            };
        }
        catch (ManagementException ex)
        {
            return Failed("system.uptime", "Systemlaufzeit konnte nicht gelesen werden: " + ex.Message, startedAt);
        }
    }

    private ActionExecutionResult ExecuteWindowsVersion()
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return new ActionExecutionResult
            {
                ActionId = "system.windows_version",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = "Windows-Version wurde aus der Registry gelesen.",
                    Values = new()
                    {
                        ["productName"] = key?.GetValue("ProductName")?.ToString(),
                        ["displayVersion"] = key?.GetValue("DisplayVersion")?.ToString(),
                        ["currentBuild"] = key?.GetValue("CurrentBuildNumber")?.ToString(),
                        ["ubr"] = key?.GetValue("UBR")?.ToString(),
                        ["edition"] = key?.GetValue("EditionID")?.ToString(),
                        ["installDate"] = key?.GetValue("InstallDate"),
                        ["registeredOwner"] = key?.GetValue("RegisteredOwner")?.ToString()
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return Failed("system.windows_version", "Windows-Version konnte nicht gelesen werden: " + ex.Message, startedAt);
        }
    }

    private ActionExecutionResult ExecutePendingReboot()
    {
        var startedAt = DateTimeOffset.Now;
        var reasons = new List<string>();

        if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS", "pending.xml")))
        {
            reasons.Add("CBS/Component Based Servicing (pending.xml)");
        }

        try
        {
            using var wuKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            if (wuKey is not null)
            {
                reasons.Add("Windows Update (RebootRequired)");
            }

            using var pfrKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            if (pfrKey?.GetValue("PendingFileRenameOperations") is not null)
            {
                reasons.Add("PendingFileRenameOperations");
            }

            using var runOnceKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce");
            if (runOnceKey?.GetValueNames().Contains("DVDRebootSignal", StringComparer.OrdinalIgnoreCase) == true)
            {
                reasons.Add("DVDRebootSignal");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Pending-Reboot-Registry-Prüfung teilweise fehlgeschlagen.");
        }

        return new ActionExecutionResult
        {
            ActionId = "system.pending_reboot",
            Success = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            Data = new DiagnosticStatusActionResult
            {
                Summary = reasons.Count > 0
                    ? $"Neustart ausstehend ({reasons.Count} Grund/Gründe)."
                    : "Kein ausstehender Neustart erkannt.",
                Values = new()
                {
                    ["rebootPending"] = reasons.Count > 0,
                    ["reasons"] = reasons
                }
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Events-Aktionen (spezialisiert)
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<ActionExecutionResult> ExecuteRecentEventsAsync(
        string actionId, string[] logNames, SinceHoursParameters parameters, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var query = new EventQuery
        {
            Levels = new[] { EventSeverity.Critical, EventSeverity.High, EventSeverity.Warning }.ToList(),
            Hours = parameters.SinceHours,
            Logs = logNames.ToList()
        };
        EventsResponse response;
        try
        {
            response = await _eventLogService.GetEventsAsync(query, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ActionId} fehlgeschlagen.", actionId);
            return Failed(actionId, "Die Ereignisabfrage ist fehlgeschlagen.", startedAt);
        }

        var events = response.Events
            .Where(e => !IsInternalDiagnosticNoise(e))
            .OrderByDescending(e => e.LastSeen)
            .Take(parameters.MaximumResults)
            .Select(e => new EventsQueryResultEvent
            {
                EventId = e.EventId,
                Provider = e.ProviderName,
                Level = e.Severity.ToString(),
                Timestamp = e.LastSeen,
                Message = Truncate(e.OriginalMessage ?? e.Summary ?? string.Empty)
            })
            .ToList();

        return new ActionExecutionResult
        {
            ActionId = actionId,
            Success = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            Data = new EventsQueryActionResult
            {
                Query = new EventsQueryParameters
                {
                    LogNames = logNames.ToList(),
                    SinceHours = parameters.SinceHours,
                    MaximumResults = parameters.MaximumResults,
                    Levels = new() { "Critical", "Error", "Warning" }
                },
                Events = events,
                Summary = new EventsQuerySummary { Total = events.Count }
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteKernelPowerEventsAsync(CancellationToken cancellationToken)
    {
        var query = new EventQuery
        {
            Levels = new[] { EventSeverity.Critical, EventSeverity.High, EventSeverity.Warning }.ToList(),
            Hours = 72,
            Logs = new[] { "System" }.ToList()
        };
        var response = await _eventLogService.GetEventsAsync(query, cancellationToken);
        var startedAt = DateTimeOffset.Now;

        var events = response.Events
            .Where(e => e.ProviderName?.Contains("Microsoft-Windows-Kernel-Power", StringComparison.OrdinalIgnoreCase) == true
                        && (e.EventId == 41 || e.EventId == 42 || e.EventId == 137))
            .OrderByDescending(e => e.LastSeen)
            .Take(50)
            .Select(e => new EventsQueryResultEvent
            {
                EventId = e.EventId,
                Provider = e.ProviderName,
                Level = e.Severity.ToString(),
                Timestamp = e.LastSeen,
                Message = Truncate(e.OriginalMessage ?? e.Summary ?? string.Empty)
            })
            .ToList();

        return new ActionExecutionResult
        {
            ActionId = "events.kernel_power",
            Success = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            Data = new EventsQueryActionResult
            {
                Query = new EventsQueryParameters { LogNames = new() { "System" }, SinceHours = 72, MaximumResults = 50 },
                Events = events,
                Summary = new EventsQuerySummary { Total = events.Count }
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteWheaEventsAsync(CancellationToken cancellationToken)
    {
        var query = new EventQuery
        {
            Levels = new[] { EventSeverity.Critical, EventSeverity.High, EventSeverity.Warning }.ToList(),
            Hours = 72,
            Logs = new[] { "System" }.ToList()
        };
        var startedAt = DateTimeOffset.Now;
        var response = await _eventLogService.GetEventsAsync(query, cancellationToken);

        var events = response.Events
            .Where(e => e.ProviderName?.Contains("WHEA", StringComparison.OrdinalIgnoreCase) == true
                        || e.ProviderName?.Contains("Microsoft-Windows-WHEA", StringComparison.OrdinalIgnoreCase) == true)
            .OrderByDescending(e => e.LastSeen)
            .Take(50)
            .Select(e => new EventsQueryResultEvent
            {
                EventId = e.EventId,
                Provider = e.ProviderName,
                Level = e.Severity.ToString(),
                Timestamp = e.LastSeen,
                Message = Truncate(e.OriginalMessage ?? e.Summary ?? string.Empty)
            })
            .ToList();

        return new ActionExecutionResult
        {
            ActionId = "events.whea",
            Success = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            Data = new EventsQueryActionResult
            {
                Query = new EventsQueryParameters { LogNames = new() { "System" }, SinceHours = 72, MaximumResults = 50 },
                Events = events,
                Summary = new EventsQuerySummary { Total = events.Count }
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteStorageEventsAsync(
        SinceHoursParameters parameters, CancellationToken cancellationToken)
    {
        var query = new EventQuery
        {
            Levels = new[] { EventSeverity.Critical, EventSeverity.High, EventSeverity.Warning }.ToList(),
            Hours = parameters.SinceHours,
            Logs = new[] { "System" }.ToList()
        };
        var startedAt = DateTimeOffset.Now;
        var response = await _eventLogService.GetEventsAsync(query, cancellationToken);

        var storageProviders = new[] { "stornvme", "disk", "Storport", "nvme", "iastora", "amdsata", "iaStorAVC", "mpio" };
        var events = response.Events
            .Where(e => storageProviders.Any(p => e.ProviderName?.Contains(p, StringComparison.OrdinalIgnoreCase) == true))
            .OrderByDescending(e => e.LastSeen)
            .Take(parameters.MaximumResults)
            .Select(e => new EventsQueryResultEvent
            {
                EventId = e.EventId,
                Provider = e.ProviderName,
                Level = e.Severity.ToString(),
                Timestamp = e.LastSeen,
                Message = Truncate(e.OriginalMessage ?? e.Summary ?? string.Empty)
            })
            .ToList();

        return new ActionExecutionResult
        {
            ActionId = "storage.events.errors",
            Success = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            Data = new EventsQueryActionResult
            {
                Query = new EventsQueryParameters { LogNames = new() { "System" }, SinceHours = parameters.SinceHours, MaximumResults = parameters.MaximumResults },
                Events = events,
                Summary = new EventsQuerySummary { Total = events.Count }
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Storage-Aktionen
    // ──────────────────────────────────────────────────────────────────────────

    private Task<ActionExecutionResult> ExecuteDiskListAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() => ExecuteDiskListCore(), cancellationToken);
    }

    private ActionExecutionResult ExecuteDiskListCore()
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            var disks = searcher.Get().OfType<ManagementObject>().Select(disk => new
            {
                index = disk["Index"],
                model = disk["Model"]?.ToString(),
                mediaType = disk["MediaType"]?.ToString(),
                interfaceType = disk["InterfaceType"]?.ToString(),
                size = disk["Size"],
                partitions = disk["Partitions"],
                status = disk["Status"]?.ToString()
            }).ToList();

            return new ActionExecutionResult
            {
                ActionId = "storage.disks.list",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = $"{disks.Count} Datenträger erkannt.",
                    Values = new() { ["disks"] = disks }
                }
            };
        }
        catch (ManagementException ex)
        {
            return Failed("storage.disks.list", "Datenträgerliste konnte nicht gelesen werden: " + ex.Message, startedAt);
        }
    }

    private ActionExecutionResult ExecuteVolumeList()
    {
        var startedAt = DateTimeOffset.Now;
        var volumes = DriveInfo.GetDrives()
            .Select(drive => new
            {
                name = drive.Name,
                label = drive.IsReady ? drive.VolumeLabel : null,
                driveType = drive.DriveType.ToString(),
                fileSystem = drive.IsReady ? drive.DriveFormat : null,
                totalBytes = drive.IsReady ? drive.TotalSize : (long?)null,
                freeBytes = drive.IsReady ? drive.AvailableFreeSpace : (long?)null,
                ready = drive.IsReady,
                usagePercent = drive.IsReady && drive.TotalSize > 0
                    ? Math.Round((drive.TotalSize - drive.AvailableFreeSpace) * 100d / drive.TotalSize, 1)
                    : (double?)null
            })
            .ToList();

        return new ActionExecutionResult
        {
            ActionId = "storage.volumes.list",
            Success = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            Data = new DiagnosticStatusActionResult
            {
                Summary = $"{volumes.Count} Laufwerke erkannt.",
                Values = new() { ["volumes"] = volumes }
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteStorageHealthAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        const string script = "[Console]::OutputEncoding=[Text.UTF8Encoding]::new(); " +
            "Get-PhysicalDisk | Select-Object FriendlyName,MediaType,OperationalStatus,HealthStatus,Size,BusType | ConvertTo-Json -Compress";
        var execution = await _processRunner.RunAsync(
            new SafeProcessRequest(powershell, new[] { "-NoProfile", "-NonInteractive", "-Command", script }, TimeSpan.FromSeconds(20)),
            cancellationToken);

        var processSucceeded = !execution.TimedOut && execution.StartError is null && execution.ExitCode == 0;
        object? disks = null;
        if (processSucceeded && !string.IsNullOrWhiteSpace(execution.StandardOutput))
        {
            try
            {
                using var doc = JsonDocument.Parse(execution.StandardOutput);
                disks = doc.RootElement.Clone();
            }
            catch (JsonException) { }
        }

        return new ActionExecutionResult
        {
            ActionId = "storage.health.basic",
            Success = processSucceeded,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Execution = execution,
            Error = processSucceeded ? null : DescribeProcessFailure(execution, "Datenträger-Gesundheitsstatus konnte nicht gelesen werden."),
            Data = new DiagnosticStatusActionResult
            {
                Summary = processSucceeded ? "Datenträger-Gesundheitsstatus wurde gelesen." : "Datenträger-Gesundheitsstatus konnte nicht gelesen werden.",
                Values = new() { ["physicalDisks"] = disks, ["rawOutput"] = execution.StandardOutput }
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Netzwerk-Aktionen
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<ActionExecutionResult> ExecuteNetworkAdaptersAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        const string script = "[Console]::OutputEncoding=[Text.UTF8Encoding]::new(); " +
            "Get-NetAdapter | Select-Object Name,InterfaceDescription,Status,MacAddress,LinkSpeed,MediaType | ConvertTo-Json -Compress";
        var execution = await _processRunner.RunAsync(
            new SafeProcessRequest(powershell, new[] { "-NoProfile", "-NonInteractive", "-Command", script }, TimeSpan.FromSeconds(20)),
            cancellationToken);

        var success = !execution.TimedOut && execution.StartError is null && execution.ExitCode == 0;
        object? adapters = null;
        if (success && !string.IsNullOrWhiteSpace(execution.StandardOutput))
        {
            try { using var doc = JsonDocument.Parse(execution.StandardOutput); adapters = doc.RootElement.Clone(); }
            catch (JsonException) { }
        }

        return new ActionExecutionResult
        {
            ActionId = "network.adapters.list",
            Success = success,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Execution = execution,
            Error = success ? null : DescribeProcessFailure(execution, "Netzwerkadapter konnten nicht gelesen werden."),
            Data = new DiagnosticStatusActionResult
            {
                Summary = success ? "Netzwerkadapter wurden gelesen." : "Netzwerkadapter konnten nicht gelesen werden.",
                Values = new() { ["adapters"] = adapters }
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteNetworkConfigurationAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        const string script = "[Console]::OutputEncoding=[Text.UTF8Encoding]::new(); " +
            "Get-NetIPConfiguration | Select-Object InterfaceAlias,InterfaceIndex,IPv4Address,IPv6Address,DNSServer,IPv4DefaultGateway | ConvertTo-Json -Compress -Depth 4";
        var execution = await _processRunner.RunAsync(
            new SafeProcessRequest(powershell, new[] { "-NoProfile", "-NonInteractive", "-Command", script }, TimeSpan.FromSeconds(20)),
            cancellationToken);

        var success = !execution.TimedOut && execution.StartError is null && execution.ExitCode == 0;
        object? config = null;
        if (success && !string.IsNullOrWhiteSpace(execution.StandardOutput))
        {
            try { using var doc = JsonDocument.Parse(execution.StandardOutput); config = doc.RootElement.Clone(); }
            catch (JsonException) { }
        }

        return new ActionExecutionResult
        {
            ActionId = "network.configuration",
            Success = success,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Execution = execution,
            Error = success ? null : DescribeProcessFailure(execution, "Netzwerkkonfiguration konnte nicht gelesen werden."),
            Data = new DiagnosticStatusActionResult
            {
                Summary = success ? "Netzwerkkonfiguration wurde gelesen." : "Netzwerkkonfiguration konnte nicht gelesen werden.",
                Values = new() { ["configuration"] = config }
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteGatewayTestAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        string? gateway = null;

        try
        {
            foreach (var iface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                var props = iface.GetIPProperties();
                var gw = props.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (gw is not null)
                {
                    gateway = gw.Address.ToString();
                    break;
                }
            }
        }
        catch { }

        if (gateway is null)
        {
            return new ActionExecutionResult
            {
                ActionId = "network.gateway.test",
                Success = false,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Error = "Kein Standard-IPv4-Gateway gefunden.",
                Data = new DiagnosticStatusActionResult
                {
                    Summary = "Kein Standard-Gateway gefunden.",
                    Values = new() { ["gateway"] = null, ["reachable"] = false }
                }
            };
        }

        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(gateway, 3000);
            var reachable = reply.Status == System.Net.NetworkInformation.IPStatus.Success;

            return new ActionExecutionResult
            {
                ActionId = "network.gateway.test",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = reachable
                        ? $"Gateway {gateway} ist erreichbar (RTT: {reply.RoundtripTime} ms)."
                        : $"Gateway {gateway} ist NICHT erreichbar (Status: {reply.Status}).",
                    Values = new()
                    {
                        ["gateway"] = gateway,
                        ["reachable"] = reachable,
                        ["status"] = reply.Status.ToString(),
                        ["rttMs"] = reply.RoundtripTime
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return Failed("network.gateway.test", $"Gateway-Test fehlgeschlagen: {ex.Message}", startedAt);
        }
    }

    private static async Task<ActionExecutionResult> ExecuteDnsResolveAsync(
        DnsResolveParameters parameters, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(parameters.Name, cancellationToken);
            return new ActionExecutionResult
            {
                ActionId = "network.dns.resolve",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = $"DNS-Name '{parameters.Name}' aufgelöst: {addresses.Length} Adresse(n).",
                    Values = new()
                    {
                        ["name"] = parameters.Name,
                        ["resolved"] = true,
                        ["addresses"] = addresses.Select(a => a.ToString()).ToArray()
                    }
                }
            };
        }
        catch (SocketException ex)
        {
            return new ActionExecutionResult
            {
                ActionId = "network.dns.resolve",
                Success = false,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Error = $"DNS-Auflösung für '{parameters.Name}' fehlgeschlagen: {ex.Message}",
                ErrorCode = "PROCESS_FAILED",
                Data = new DiagnosticStatusActionResult
                {
                    Summary = $"DNS-Name '{parameters.Name}' konnte nicht aufgelöst werden.",
                    Values = new() { ["name"] = parameters.Name, ["resolved"] = false, ["socketError"] = ex.SocketErrorCode.ToString() }
                }
            };
        }
    }

    private static async Task<ActionExecutionResult> ExecutePortTestAsync(
        PortTestParameters parameters, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(parameters.Host, parameters.Port, cts.Token);
            return new ActionExecutionResult
            {
                ActionId = "network.port.test",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = $"TCP-Port {parameters.Port} auf {parameters.Host} ist erreichbar.",
                    Values = new() { ["host"] = parameters.Host, ["port"] = parameters.Port, ["reachable"] = true }
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ActionExecutionResult
            {
                ActionId = "network.port.test",
                Success = false,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Error = $"TCP-Port {parameters.Port} auf {parameters.Host} ist NICHT erreichbar: {ex.Message}",
                ErrorCode = "PROCESS_FAILED",
                Data = new DiagnosticStatusActionResult
                {
                    Summary = $"TCP-Port {parameters.Port} auf {parameters.Host} nicht erreichbar.",
                    Values = new() { ["host"] = parameters.Host, ["port"] = parameters.Port, ["reachable"] = false }
                }
            };
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Prozess-Aktionen
    // ──────────────────────────────────────────────────────────────────────────

    private static ActionExecutionResult ExecuteProcessList(ProcessListParameters parameters, string sortBy)
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            var allProcesses = System.Diagnostics.Process.GetProcesses();
            var processData = allProcesses
                .Select(p =>
                {
                    try
                    {
                        return new
                        {
                            pid = p.Id,
                            name = p.ProcessName,
                            cpuMs = default(long?),  // Echte CPU-Zeit nicht ohne Polling messbar
                            workingSetBytes = p.WorkingSet64,
                            handles = (int?)p.HandleCount
                        };
                    }
                    catch
                    {
                        return new { pid = p.Id, name = p.ProcessName, cpuMs = (long?)null, workingSetBytes = (long)0, handles = (int?)null };
                    }
                })
                .ToList();

            var sorted = sortBy switch
            {
                "memory" => processData.OrderByDescending(p => p.workingSetBytes).Take(parameters.Top).ToList(),
                _ => processData.OrderBy(p => p.name).Take(parameters.Top).ToList()
            };

            return new ActionExecutionResult
            {
                ActionId = sortBy switch { "cpu" => "process.cpu_top", "memory" => "process.memory_top", _ => "process.list" },
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = $"{sorted.Count} Prozesse gelesen (von {processData.Count} gesamt).",
                    Values = new() { ["processes"] = sorted, ["total"] = processData.Count }
                }
            };
        }
        catch (Exception ex)
        {
            return Failed(sortBy == "memory" ? "process.memory_top" : "process.list", "Prozessliste konnte nicht gelesen werden: " + ex.Message, startedAt);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dienst-Aktionen
    // ──────────────────────────────────────────────────────────────────────────

    private ActionExecutionResult ExecuteServiceList()
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DisplayName, State, StartMode, PathName FROM Win32_Service");
            var services = searcher.Get().OfType<ManagementObject>()
                .Select(svc => new
                {
                    name = svc["Name"]?.ToString(),
                    displayName = svc["DisplayName"]?.ToString(),
                    state = svc["State"]?.ToString(),
                    startMode = svc["StartMode"]?.ToString()
                })
                .OrderBy(s => s.name)
                .ToList();

            return new ActionExecutionResult
            {
                ActionId = "service.list",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = $"{services.Count} Dienste gelesen.",
                    Values = new() { ["services"] = services }
                }
            };
        }
        catch (ManagementException ex)
        {
            return Failed("service.list", "Dienstliste konnte nicht gelesen werden: " + ex.Message, startedAt);
        }
    }

    private ActionExecutionResult ExecuteServiceStatus(ServiceStatusParameters parameters)
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT Name, DisplayName, State, StartMode, Description FROM Win32_Service WHERE Name='{parameters.ServiceName}'");
            var svc = searcher.Get().OfType<ManagementObject>().FirstOrDefault();

            if (svc is null)
            {
                return new ActionExecutionResult
                {
                    ActionId = "service.status",
                    Success = false,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.Now,
                    Error = $"Dienst '{parameters.ServiceName}' wurde nicht gefunden.",
                    ErrorCode = "PROCESS_FAILED",
                    Data = new DiagnosticStatusActionResult
                    {
                        Summary = $"Dienst '{parameters.ServiceName}' nicht gefunden.",
                        Values = new() { ["serviceName"] = parameters.ServiceName, ["found"] = false }
                    }
                };
            }

            return new ActionExecutionResult
            {
                ActionId = "service.status",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = "Dienst " + parameters.ServiceName + ": " + svc["State"] + " (" + svc["StartMode"] + ").",
                    Values = new()
                    {
                        ["serviceName"] = svc["Name"]?.ToString(),
                        ["displayName"] = svc["DisplayName"]?.ToString(),
                        ["state"] = svc["State"]?.ToString(),
                        ["startMode"] = svc["StartMode"]?.ToString(),
                        ["found"] = true
                    }
                }
            };
        }
        catch (ManagementException ex)
        {
            return Failed("service.status", $"Dienststatus konnte nicht gelesen werden: " + ex.Message, startedAt);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Domänen-Aktionen
    // ──────────────────────────────────────────────────────────────────────────

    private ActionExecutionResult ExecuteDomainStatus()
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT PartOfDomain, Domain, DNSHostName, Workgroup FROM Win32_ComputerSystem");
            var cs = searcher.Get().OfType<ManagementObject>().FirstOrDefault();

            var partOfDomain = cs?["PartOfDomain"] is true;
            var domain = cs?["Domain"]?.ToString();
            var workgroup = cs?["Workgroup"]?.ToString();

            return new ActionExecutionResult
            {
                ActionId = "domain.status",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Data = new DiagnosticStatusActionResult
                {
                    Summary = partOfDomain
                        ? $"Rechner ist Mitglied der Domäne {domain}."
                        : $"Rechner ist kein Domänenmitglied (Arbeitsgruppe: {workgroup}).",
                    Values = new()
                    {
                        ["partOfDomain"] = partOfDomain,
                        ["domain"] = domain,
                        ["workgroup"] = workgroup,
                        ["dnsHostName"] = cs?["DNSHostName"]?.ToString()
                    }
                }
            };
        }
        catch (ManagementException ex)
        {
            return Failed("domain.status", "Domänenstatus konnte nicht gelesen werden: " + ex.Message, startedAt);
        }
    }

    private async Task<ActionExecutionResult> ExecuteDcDiscoveryAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var nlltest = Path.Combine(Environment.SystemDirectory, "nltest.exe");
        if (!File.Exists(nlltest))
        {
            return Failed("domain.dc_discovery", "nltest.exe nicht gefunden.", startedAt);
        }

        var execution = await _processRunner.RunAsync(
            new SafeProcessRequest(nlltest, new[] { "/dsgetdc:" }, TimeSpan.FromSeconds(15)),
            cancellationToken);

        var success = !execution.TimedOut && execution.StartError is null && execution.ExitCode == 0;
        return new ActionExecutionResult
        {
            ActionId = "domain.dc_discovery",
            Success = success,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Execution = execution,
            Error = success ? null : DescribeProcessFailure(execution, "DC-Suche fehlgeschlagen."),
            Data = new DiagnosticStatusActionResult
            {
                Summary = success ? "Domänencontroller gefunden." : "Domänencontroller-Suche fehlgeschlagen.",
                Values = new() { ["rawOutput"] = execution.StandardOutput, ["stderr"] = execution.StandardError }
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteSecureChannelTestAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var nltest = Path.Combine(Environment.SystemDirectory, "nltest.exe");
        if (!File.Exists(nltest))
        {
            return Failed("domain.secure_channel.test", "nltest.exe nicht gefunden.", startedAt);
        }

        var execution = await _processRunner.RunAsync(
            new SafeProcessRequest(nltest, new[] { "/sc_verify:" }, TimeSpan.FromSeconds(20)),
            cancellationToken);

        var success = !execution.TimedOut && execution.StartError is null && execution.ExitCode == 0;
        return new ActionExecutionResult
        {
            ActionId = "domain.secure_channel.test",
            Success = success,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Execution = execution,
            Error = success ? null : DescribeProcessFailure(execution, "Secure-Channel-Prüfung fehlgeschlagen."),
            Data = new DiagnosticStatusActionResult
            {
                Summary = success ? "Sicherer Kanal ist intakt." : "Sicherer Kanal konnte nicht geprüft werden.",
                Values = new() { ["rawOutput"] = execution.StandardOutput, ["stderr"] = execution.StandardError }
            }
        };
    }

    private static string? ResolveWingetPath()
    {
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in pathEntries.Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps")))
        {
            var candidate = Path.Combine(entry, "winget.exe");
            if (File.Exists(candidate) && Path.GetFileName(candidate).Equals("winget.exe", StringComparison.OrdinalIgnoreCase)) return candidate;
        }
        return null;
    }

    private static List<WingetSourceItem> ParseSources(string output)
    {
        var sources = new List<WingetSourceItem>();
        foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var cells = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length >= 2 && cells[1].StartsWith("http", StringComparison.OrdinalIgnoreCase) && !cells[0].Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(new WingetSourceItem { Name = cells[0], Argument = cells[1], Type = cells.Length > 3 ? cells[2] : null });
            }
        }
        return sources;
    }

    private void Audit(ActionExecutionResult result)
    {
        var execution = result.Execution;
        _logger.LogInformation("Diagnostic audit: ActionId={ActionId} User={User} Success={Success} StartedAt={StartedAt} CompletedAt={CompletedAt} DurationMs={DurationMs} Program={Program} Arguments={Arguments} ExitCode={ExitCode} TimedOut={TimedOut}",
            result.ActionId, CurrentUserContext(), result.Success, result.StartedAt, result.CompletedAt,
            execution?.DurationMs ?? (long)(result.CompletedAt - result.StartedAt).TotalMilliseconds,
            execution?.Program, execution is null ? null : string.Join(" ", execution.Arguments), execution?.ExitCode, execution?.TimedOut);
    }

    private static ActionExecutionResult Failed(string actionId, string error, DateTimeOffset? startedAt = null, string? errorCode = null) =>
        new() { ActionId = actionId, Success = false, StartedAt = startedAt ?? DateTimeOffset.Now, CompletedAt = DateTimeOffset.Now, Error = error, ErrorCode = errorCode };
    private static ProcessExecutionDetails UnavailableExecution(string program, IReadOnlyList<string> arguments, DateTimeOffset startedAt, string error) => new() { Program = program, Arguments = arguments.ToList(), StartedAt = startedAt, CompletedAt = DateTimeOffset.Now, DurationMs = (long)(DateTimeOffset.Now - startedAt).TotalMilliseconds, ExitCode = -1, StartError = error };
    private static string DescribeProcessFailure(ProcessExecutionDetails execution, string fallback) => execution.TimedOut ? "Zeitüberschreitung bei der Ausführung." : execution.StartError ?? (!string.IsNullOrWhiteSpace(execution.StandardError) ? execution.StandardError.Trim() : $"{fallback} Exitcode: {execution.ExitCode}.");
    private static string CurrentUserContext() => WindowsIdentity.GetCurrent()?.Name ?? Environment.UserName;
    private static EventSeverity MapLevel(string level) => level switch { "Critical" => EventSeverity.Critical, "Error" => EventSeverity.High, _ => EventSeverity.Warning };
    private static string Truncate(string text) => text.Length <= MessageMaxLength ? text : text[..MessageMaxLength].TrimEnd() + " …";
    private static bool IsInternalDiagnosticNoise(EventItem eventItem)
    {
        var provider = eventItem.ProviderName ?? string.Empty;
        var message = eventItem.OriginalMessage ?? eventItem.Summary ?? string.Empty;
        return provider.Contains("WindowsDiagnosticApp", StringComparison.OrdinalIgnoreCase) || message.Contains("WindowsDiagnosticApp", StringComparison.OrdinalIgnoreCase) || provider.Contains("OllamaService", StringComparison.OrdinalIgnoreCase) || provider.Contains("System.Diagnostics", StringComparison.OrdinalIgnoreCase) || (provider.Contains(".NET Runtime", StringComparison.OrdinalIgnoreCase) && message.Contains("WindowsDiagnosticApp", StringComparison.OrdinalIgnoreCase));
    }
}

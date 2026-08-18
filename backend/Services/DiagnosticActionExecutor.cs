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
            "winget.status" => await ExecuteWingetStatusAsync(cancellationToken),
            "winget.sources.list" => await ExecuteWingetSourcesAsync(cancellationToken),
            "appinstaller.status" => await ExecuteAppInstallerStatusAsync(cancellationToken),
            "windowsupdate.status" => ExecuteWindowsUpdateStatus(),
            "storage.summary" or "system.storage.summary" => ExecuteStorageSummary(),
            "network.microsoftEndpoints" or "network.basicConnectivity" => await ExecuteConnectivityAsync(cancellationToken),
            _ => Failed(actionId, "Diese Diagnoseaktion ist noch nicht implementiert.")
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

    private static ActionExecutionResult Failed(string actionId, string error, DateTimeOffset? startedAt = null) => new() { ActionId = actionId, Success = false, StartedAt = startedAt ?? DateTimeOffset.Now, CompletedAt = DateTimeOffset.Now, Error = error };
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

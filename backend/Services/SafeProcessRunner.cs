using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

public sealed record SafeProcessRequest(string Program, IReadOnlyList<string> Arguments, TimeSpan Timeout);

public interface ISafeProcessRunner
{
    Task<ProcessExecutionDetails> RunAsync(SafeProcessRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Startet nur fest verdrahtete Diagnoseprogramme. Argumente werden nie als Shell-String
/// interpretiert, sondern einzeln über <see cref="ProcessStartInfo.ArgumentList"/> übergeben.
/// </summary>
public sealed class SafeProcessRunner : ISafeProcessRunner
{
    private const int MaxCapturedCharacters = 64 * 1024;
    private static readonly HashSet<string> AllowedPrograms = new(StringComparer.OrdinalIgnoreCase)
    {
        "winget.exe", "powershell.exe", "pwsh.exe",
        "ipconfig.exe", "ping.exe", "nslookup.exe", "tracert.exe",
        "netstat.exe", "netsh.exe", "net.exe", "sc.exe",
        "tasklist.exe", "systeminfo.exe", "wevtutil.exe",
        "chkdsk.exe", "fsutil.exe", "nltest.exe"
    };

    public async Task<ProcessExecutionDetails> RunAsync(SafeProcessRequest request, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(request.Program) ||
            !AllowedPrograms.Contains(Path.GetFileName(request.Program)))
        {
            var now = DateTimeOffset.Now;
            return FailedStart(request, now, "Das angeforderte Diagnoseprogramm ist nicht freigegeben.");
        }

        var startedAt = DateTimeOffset.Now;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = request.Program,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in request.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return FailedStart(request, startedAt, "Der Diagnoseprozess konnte nicht gestartet werden.");
            }

            var outputTask = ReadBoundedAsync(process.StandardOutput);
            var errorTask = ReadBoundedAsync(process.StandardError);
            using var timeout = new CancellationTokenSource(request.Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            try
            {
                await process.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                KillProcessTree(process);
                var output = await outputTask;
                var error = await errorTask;
                return CreateResult(request, startedAt, -1, output, error, timedOut: true, startError: null);
            }
            catch (OperationCanceledException)
            {
                KillProcessTree(process);
                await Task.WhenAll(outputTask, errorTask);
                throw;
            }

            var completedOutput = await outputTask;
            var completedError = await errorTask;
            return CreateResult(request, startedAt, process.ExitCode, completedOutput, completedError, timedOut: false, startError: null);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or UnauthorizedAccessException)
        {
            return FailedStart(request, startedAt, ex.Message);
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Der Prozess wurde zwischen Prüfung und Abbruch beendet.
        }
    }

    private static async Task<(string Text, bool Truncated)> ReadBoundedAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        var output = new StringBuilder();
        var truncated = false;
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            var remaining = MaxCapturedCharacters - output.Length;
            if (remaining > 0)
            {
                output.Append(buffer, 0, Math.Min(read, remaining));
            }
            if (read > remaining)
            {
                truncated = true;
            }
        }
        return (output.ToString(), truncated);
    }

    private static ProcessExecutionDetails FailedStart(SafeProcessRequest request, DateTimeOffset startedAt, string error) =>
        new()
        {
            Program = request.Program,
            Arguments = request.Arguments.ToList(),
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            DurationMs = (long)(DateTimeOffset.Now - startedAt).TotalMilliseconds,
            ExitCode = -1,
            StartError = error
        };

    private static ProcessExecutionDetails CreateResult(
        SafeProcessRequest request,
        DateTimeOffset startedAt,
        int exitCode,
        (string Text, bool Truncated) output,
        (string Text, bool Truncated) error,
        bool timedOut,
        string? startError)
    {
        var completedAt = DateTimeOffset.Now;
        return new ProcessExecutionDetails
        {
            Program = request.Program,
            Arguments = request.Arguments.ToList(),
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DurationMs = (long)(completedAt - startedAt).TotalMilliseconds,
            ExitCode = exitCode,
            StandardOutput = output.Text,
            StandardError = error.Text,
            OutputTruncated = output.Truncated || error.Truncated,
            TimedOut = timedOut,
            StartError = startError
        };
    }
}
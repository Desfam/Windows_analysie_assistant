using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class SafeProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_RejectsUnapprovedExecutable()
    {
        var runner = new SafeProcessRunner();

        var result = await runner.RunAsync(
            new SafeProcessRequest(Path.Combine(Environment.SystemDirectory, "cmd.exe"), new[] { "/c", "echo blocked" }, TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        Assert.Equal(-1, result.ExitCode);
        Assert.NotNull(result.StartError);
    }

    [Fact]
    public async Task RunAsync_UsesArgumentListAndCapturesOutput()
    {
        var runner = new SafeProcessRunner();
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

        var result = await runner.RunAsync(
            new SafeProcessRequest(powershell, new[] { "-NoProfile", "-NonInteractive", "-Command", "Write-Output runner-ok" }, TimeSpan.FromSeconds(5)),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("runner-ok", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Write-Output runner-ok", result.Arguments[^1]);
    }

    [Fact]
    public async Task RunAsync_KillsTimedOutProcessTree()
    {
        var runner = new SafeProcessRunner();
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

        var result = await runner.RunAsync(
            new SafeProcessRequest(powershell, new[] { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 5" }, TimeSpan.FromMilliseconds(100)),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
    }
}
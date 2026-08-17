using WindowsDiagnosticApp.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class HealthEvaluatorTests
{
    private readonly HealthEvaluator _evaluator = new(new ThresholdOptions());

    [Theory]
    [InlineData(50, HealthStatus.Normal)]
    [InlineData(84.9, HealthStatus.Normal)]
    [InlineData(85, HealthStatus.Warning)]
    [InlineData(94.9, HealthStatus.Warning)]
    [InlineData(95, HealthStatus.Critical)]
    [InlineData(99, HealthStatus.Critical)]
    public void EvaluateRam_UsesConfiguredThresholds(double usage, HealthStatus expected)
    {
        Assert.Equal(expected, _evaluator.EvaluateRam(usage));
    }

    [Theory]
    [InlineData(50, HealthStatus.Normal)]
    [InlineData(15.1, HealthStatus.Normal)]
    [InlineData(15, HealthStatus.Warning)]
    [InlineData(5.1, HealthStatus.Warning)]
    [InlineData(5, HealthStatus.Critical)]
    [InlineData(1, HealthStatus.Critical)]
    public void EvaluateDisk_UsesConfiguredThresholds(double freePercent, HealthStatus expected)
    {
        Assert.Equal(expected, _evaluator.EvaluateDiskByFreePercent(freePercent));
    }
}

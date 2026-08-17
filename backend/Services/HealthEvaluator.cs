using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Options;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Bewertet Auslastungswerte anhand der zentral konfigurierten Grenzwerte.
/// </summary>
public sealed class HealthEvaluator
{
    private readonly ThresholdOptions _options;

    public HealthEvaluator(IOptions<ThresholdOptions> options)
    {
        _options = options.Value;
    }

    public HealthEvaluator(ThresholdOptions options)
    {
        _options = options;
    }

    /// <summary>Bewertet die RAM-Auslastung in Prozent.</summary>
    public HealthStatus EvaluateRam(double usagePercent)
    {
        if (usagePercent >= _options.RamCriticalPercent)
        {
            return HealthStatus.Critical;
        }

        if (usagePercent >= _options.RamWarningPercent)
        {
            return HealthStatus.Warning;
        }

        return HealthStatus.Normal;
    }

    /// <summary>Bewertet ein Laufwerk anhand des freien Speichers in Prozent.</summary>
    public HealthStatus EvaluateDiskByFreePercent(double freePercent)
    {
        if (freePercent <= _options.DiskFreeCriticalPercent)
        {
            return HealthStatus.Critical;
        }

        if (freePercent <= _options.DiskFreeWarningPercent)
        {
            return HealthStatus.Warning;
        }

        return HealthStatus.Normal;
    }
}

using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using Microsoft.Win32;
using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Ermittelt lokale Rechnerinformationen über native Windows-Schnittstellen (WMI,
/// Registry, DriveInfo). Alle Abfragen sind ausschließlich lesend. Einzelne
/// fehlgeschlagene Abfragen liefern <c>null</c>, damit die Gesamtabfrage nicht scheitert.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SystemInfoService : ISystemInfoService
{
    private readonly HealthEvaluator _evaluator;
    private readonly ILogger<SystemInfoService> _logger;

    public SystemInfoService(HealthEvaluator evaluator, ILogger<SystemInfoService> logger)
    {
        _evaluator = evaluator;
        _logger = logger;
    }

    public Task<SystemSummary> GetSummaryAsync(CancellationToken cancellationToken) =>
        Task.Run(BuildSummary, cancellationToken);

    public Task<CpuInfo> GetCpuAsync(CancellationToken cancellationToken) =>
        Task.Run(BuildCpu, cancellationToken);

    public Task<MemoryInfo> GetMemoryAsync(CancellationToken cancellationToken) =>
        Task.Run(BuildMemory, cancellationToken);

    public Task<IReadOnlyList<GpuInfo>> GetGpusAsync(CancellationToken cancellationToken) =>
        Task.Run(BuildGpus, cancellationToken);

    public Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken) =>
        Task.Run(BuildDisks, cancellationToken);

    public Task<WindowsInfo> GetWindowsAsync(CancellationToken cancellationToken) =>
        Task.Run(BuildWindows, cancellationToken);

    private SystemSummary BuildSummary()
    {
        var cs = QueryFirst("SELECT Manufacturer, Model, SystemType, UserName FROM Win32_ComputerSystem");
        var os = QueryFirst("SELECT LastBootUpTime FROM Win32_OperatingSystem");

        DateTimeOffset? boot = null;
        string? uptime = null;
        var bootRaw = GetString(os, "LastBootUpTime");
        if (bootRaw is not null)
        {
            try
            {
                var bootTime = ManagementDateTimeConverter.ToDateTime(bootRaw);
                boot = bootTime;
                uptime = FormatUptime(DateTime.Now - bootTime);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Startzeit konnte nicht ausgewertet werden.");
            }
        }

        return new SystemSummary
        {
            MachineName = Environment.MachineName,
            Manufacturer = GetString(cs, "Manufacturer"),
            Model = GetString(cs, "Model"),
            SystemType = GetString(cs, "SystemType"),
            LastBootTime = boot,
            Uptime = uptime,
            CurrentUser = GetString(cs, "UserName") ?? Environment.UserName,
            Status = HealthStatus.Normal
        };
    }

    private CpuInfo BuildCpu()
    {
        var cpu = QueryFirst(
            "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");

        double? usage = null;
        var perf = QueryFirst(
            "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
        var usageRaw = GetValue(perf, "PercentProcessorTime");
        if (usageRaw is not null)
        {
            usage = System.Convert.ToDouble(usageRaw, CultureInfo.InvariantCulture);
        }

        double? clockGhz = null;
        var clockRaw = GetValue(cpu, "MaxClockSpeed");
        if (clockRaw is not null)
        {
            clockGhz = Math.Round(System.Convert.ToDouble(clockRaw, CultureInfo.InvariantCulture) / 1000.0, 2);
        }

        return new CpuInfo
        {
            Manufacturer = GetString(cpu, "Manufacturer"),
            Model = GetString(cpu, "Name"),
            PhysicalCores = GetInt(cpu, "NumberOfCores"),
            LogicalProcessors = GetInt(cpu, "NumberOfLogicalProcessors"),
            UsagePercent = usage,
            MaxClockSpeedGhz = clockGhz,
            Status = HealthStatus.NotChecked
        };
    }

    private MemoryInfo BuildMemory()
    {
        var os = QueryFirst("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
        var totalKb = GetValue(os, "TotalVisibleMemorySize");
        var freeKb = GetValue(os, "FreePhysicalMemory");

        if (totalKb is null || freeKb is null)
        {
            return new MemoryInfo { Status = HealthStatus.NotChecked };
        }

        var total = System.Convert.ToDouble(totalKb, CultureInfo.InvariantCulture) * 1024.0;
        var free = System.Convert.ToDouble(freeKb, CultureInfo.InvariantCulture) * 1024.0;
        var used = total - free;
        var percent = total > 0 ? Math.Round(used / total * 100.0, 1) : 0;

        return new MemoryInfo
        {
            TotalBytes = total,
            UsedBytes = used,
            AvailableBytes = free,
            UsagePercent = percent,
            Status = _evaluator.EvaluateRam(percent)
        };
    }

    private IReadOnlyList<GpuInfo> BuildGpus()
    {
        var list = new List<GpuInfo>();
        foreach (var gpu in QueryAll(
            "SELECT Name, AdapterCompatibility, DriverVersion, AdapterRAM FROM Win32_VideoController"))
        {
            double? vram = null;
            var ramRaw = GetValue(gpu, "AdapterRAM");
            if (ramRaw is not null)
            {
                var value = System.Convert.ToInt64(ramRaw, CultureInfo.InvariantCulture);
                if (value > 0)
                {
                    vram = value;
                }
            }

            list.Add(new GpuInfo
            {
                Name = GetString(gpu, "Name"),
                Manufacturer = GetString(gpu, "AdapterCompatibility"),
                DriverVersion = GetString(gpu, "DriverVersion"),
                VideoMemoryBytes = vram,
                Status = HealthStatus.NotChecked
            });

            gpu.Dispose();
        }

        return list;
    }

    private IReadOnlyList<DiskInfo> BuildDisks()
    {
        var list = new List<DiskInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
            {
                continue;
            }

            try
            {
                double total = drive.TotalSize;
                double free = drive.TotalFreeSpace;
                double used = total - free;
                var percent = total > 0 ? Math.Round(used / total * 100.0, 1) : 0;
                var freePercent = total > 0 ? free / total * 100.0 : 100;

                list.Add(new DiskInfo
                {
                    DriveLetter = drive.Name.TrimEnd('\\'),
                    FileSystem = drive.DriveFormat,
                    TotalBytes = total,
                    UsedBytes = used,
                    FreeBytes = free,
                    UsagePercent = percent,
                    Status = _evaluator.EvaluateDiskByFreePercent(freePercent)
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Laufwerk {Drive} konnte nicht gelesen werden.", drive.Name);
            }
        }

        return list;
    }

    private WindowsInfo BuildWindows()
    {
        string? edition = null;
        string? version = null;
        string? build = null;
        DateTimeOffset? installDate = null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is not null)
            {
                edition = key.GetValue("ProductName") as string;
                version = key.GetValue("DisplayVersion") as string ?? key.GetValue("ReleaseId") as string;
                var currentBuild = key.GetValue("CurrentBuild") as string;
                var ubr = key.GetValue("UBR");
                build = ubr is not null ? $"{currentBuild}.{ubr}" : currentBuild;

                if (key.GetValue("InstallDate") is int unix)
                {
                    installDate = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Windows-Registrierungsdaten konnten nicht gelesen werden.");
        }

        return new WindowsInfo
        {
            Edition = edition,
            Version = version,
            Build = build,
            InstallDate = installDate,
            RecentUpdates = ReadRecentUpdates(),
            PendingUpdateCount = null,
            Status = HealthStatus.NotChecked
        };
    }

    private List<WindowsUpdateEntry> ReadRecentUpdates()
    {
        var list = new List<WindowsUpdateEntry>();
        try
        {
            foreach (var qfe in QueryAll("SELECT HotFixID, InstalledOn FROM Win32_QuickFixEngineering"))
            {
                DateTimeOffset? installed = null;
                var raw = GetString(qfe, "InstalledOn");
                if (raw is not null && DateTime.TryParse(
                    raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
                {
                    installed = parsed;
                }

                list.Add(new WindowsUpdateEntry
                {
                    Id = GetString(qfe, "HotFixID"),
                    InstalledOn = installed
                });

                qfe.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Installierte Updates konnten nicht gelesen werden.");
        }

        return list
            .OrderByDescending(u => u.InstalledOn ?? DateTimeOffset.MinValue)
            .Take(10)
            .ToList();
    }

    private ManagementObject? QueryFirst(string wql)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            foreach (var item in searcher.Get())
            {
                return (ManagementObject)item;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WMI-Abfrage fehlgeschlagen: {Query}", wql);
        }

        return null;
    }

    private IEnumerable<ManagementObject> QueryAll(string wql)
    {
        ManagementObjectCollection? collection = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            collection = searcher.Get();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WMI-Abfrage fehlgeschlagen: {Query}", wql);
        }

        if (collection is null)
        {
            yield break;
        }

        foreach (var item in collection)
        {
            yield return (ManagementObject)item;
        }
    }

    private static string? GetString(ManagementBaseObject? obj, string property)
    {
        var value = GetValue(obj, property);
        var text = value?.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static int? GetInt(ManagementBaseObject? obj, string property)
    {
        var value = GetValue(obj, property);
        return value is null ? null : System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static object? GetValue(ManagementBaseObject? obj, string property)
    {
        if (obj is null)
        {
            return null;
        }

        try
        {
            return obj[property];
        }
        catch
        {
            return null;
        }
    }

    private static string FormatUptime(TimeSpan span)
    {
        if (span.TotalDays >= 1)
        {
            return $"{(int)span.TotalDays} Tage, {span.Hours} Std., {span.Minutes} Min.";
        }

        return $"{span.Hours} Std., {span.Minutes} Min.";
    }
}

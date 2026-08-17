using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

public interface ISystemInfoService
{
    Task<SystemSummary> GetSummaryAsync(CancellationToken cancellationToken);
    Task<CpuInfo> GetCpuAsync(CancellationToken cancellationToken);
    Task<MemoryInfo> GetMemoryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GpuInfo>> GetGpusAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken);
    Task<WindowsInfo> GetWindowsAsync(CancellationToken cancellationToken);
}

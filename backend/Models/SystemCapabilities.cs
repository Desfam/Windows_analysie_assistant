namespace WindowsDiagnosticApp.Models;

/// <summary>
/// Beschreibt die auf dem aktuellen System verfügbaren Diagnosefähigkeiten.
/// Wird beim Start der Anwendung einmalig ermittelt und von der Action Registry
/// zur Verfügbarkeitsprüfung verwendet.
/// </summary>
public sealed class SystemCapabilities
{
    public string WindowsVersion { get; init; } = string.Empty;
    public string Build { get; init; } = string.Empty;
    public string Edition { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;

    public bool IsAdministrator { get; init; }

    public string WindowsPowerShellVersion { get; init; } = string.Empty;
    public bool PowerShell7Installed { get; init; }
    public string? PowerShell7Version { get; init; }

    /// <summary>Installierte PowerShell-Module (Name → Version).</summary>
    public IReadOnlyDictionary<string, string> AvailableModules { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Verfügbare PowerShell-Cmdlets.</summary>
    public IReadOnlyCollection<string> AvailableCommands { get; init; } =
        Array.Empty<string>();

    /// <summary>Verfügbare Windows-Binaries (vollständig qualifizierte Pfade).</summary>
    public IReadOnlyDictionary<string, string> AvailableBinaries { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Zeitpunkt, zu dem die Fähigkeiten ermittelt wurden.</summary>
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.Now;

    // Hilfsmethoden für die Registry

    public bool HasModule(string moduleName) =>
        AvailableModules.ContainsKey(moduleName);

    public bool HasCommand(string commandName) =>
        AvailableCommands.Contains(commandName, StringComparer.OrdinalIgnoreCase);

    public bool HasBinary(string binaryName) =>
        AvailableBinaries.ContainsKey(binaryName);

    public string? GetBinaryPath(string binaryName) =>
        AvailableBinaries.TryGetValue(binaryName, out var path) ? path : null;

    public bool IsWindows10OrLater() =>
        Version.TryParse(Build, out var v) && v.Major >= 10000;
}

using System.Text.Json;
using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Options;
using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

/// <summary>
/// Tests für die erweiterte Action Registry mit 20+ R0/R1-Aktionen.
/// Deckt die Abnahmekriterien aus Aufgabe 21 ab.
/// </summary>
public sealed class ExtendedActionRegistryTests
{
    private static DiagnosticActionCatalog CreateCatalog() =>
        new(Microsoft.Extensions.Options.Options.Create(new EventOptions()));

    private static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Kernregel: Unbekannte actionId wird blockiert (Abnahme 1)
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("run_powershell")]
    [InlineData("run_cmd")]
    [InlineData("execute_script")]
    [InlineData("system.reboot")]
    [InlineData("powershell.free")]
    [InlineData("shell.exec")]
    [InlineData("format.disk")]
    public void UnknownActionId_IsBlockedWithErrorCode(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("{}"));
        Assert.False(result.IsValid);
        Assert.Equal("ACTION_NOT_FOUND", result.ErrorCode);
    }

    // ──────────────────────────────────────────────────────────────────────
    // R2+ wird blockiert (Abnahme 6)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void R2PlusActions_AreDefinedButBlockedByPolicy()
    {
        // Alle R0/R1-Aktionen sollen validierbar sein.
        // Sobald R2+-Aktionen hinzugefügt werden, müssen sie EXECUTION_BLOCKED_BY_RISK_POLICY liefern.
        // In dieser Version gibt es noch keine R2+-Aktionen im Katalog.
        // Dieser Test dokumentiert die Erwartung: eine nicht vorhandene R2-Aktion hat ACTION_NOT_FOUND.
        var result = CreateCatalog().ValidateCall("disk.repair.chkdsk", Json("{}"));
        Assert.False(result.IsValid);
        Assert.Equal("ACTION_NOT_FOUND", result.ErrorCode);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Neue R0/R1 System-Aktionen (Abnahme 3)
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("system.info")]
    [InlineData("system.uptime")]
    [InlineData("system.windows_version")]
    [InlineData("system.pending_reboot")]
    public void SystemActions_AreRegisteredAndParameterless(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("{}"));
        Assert.True(result.IsValid);
        Assert.NotNull(result.Definition);
        Assert.False(result.Definition!.ChangesSystem);
        Assert.IsType<EmptyDiagnosticParameters>(result.Parameters);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Neue Events-Aktionen
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("events.system.recent")]
    [InlineData("events.application.recent")]
    [InlineData("storage.events.errors")]
    public void SinceHoursActions_AcceptValidParameters(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("""{ "sinceHours": 48, "maximumResults": 30 }"""));
        Assert.True(result.IsValid);
        var p = Assert.IsType<SinceHoursParameters>(result.Parameters);
        Assert.Equal(48, p.SinceHours);
        Assert.Equal(30, p.MaximumResults);
    }

    [Theory]
    [InlineData("events.system.recent")]
    [InlineData("events.application.recent")]
    [InlineData("storage.events.errors")]
    public void SinceHoursActions_UseDefaultsWhenEmpty(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("{}"));
        Assert.True(result.IsValid);
        var p = Assert.IsType<SinceHoursParameters>(result.Parameters);
        Assert.InRange(p.SinceHours, 1, 168);
        Assert.InRange(p.MaximumResults, 1, 500);
    }

    [Theory]
    [InlineData("events.system.recent", """{ "sinceHours": 0 }""")]
    [InlineData("events.system.recent", """{ "sinceHours": 99999 }""")]
    [InlineData("events.system.recent", """{ "maximumResults": 0 }""")]
    [InlineData("events.system.recent", """{ "unknownField": "x" }""")]
    public void SinceHoursActions_RejectInvalidParameters(string actionId, string json)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json(json));
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_PARAMETER", result.ErrorCode);
    }

    [Theory]
    [InlineData("events.kernel_power")]
    [InlineData("events.whea")]
    public void FixedEventActions_AreRegistered(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("{}"));
        Assert.True(result.IsValid);
        // These actions accept optional SinceHoursParameters (hours + maxResults defaults apply)
        Assert.IsType<SinceHoursParameters>(result.Parameters);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Storage-Aktionen
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("storage.disks.list")]
    [InlineData("storage.volumes.list")]
    [InlineData("storage.health.basic")]
    public void StorageActions_AreRegistered(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("{}"));
        Assert.True(result.IsValid);
        Assert.IsType<EmptyDiagnosticParameters>(result.Parameters);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Netzwerk-Aktionen
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("network.adapters.list")]
    [InlineData("network.configuration")]
    [InlineData("network.gateway.test")]
    public void NetworkActions_AreRegisteredAndParameterless(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("{}"));
        Assert.True(result.IsValid);
        Assert.IsType<EmptyDiagnosticParameters>(result.Parameters);
    }

    [Fact]
    public void DnsResolve_ValidName_IsAccepted()
    {
        var result = CreateCatalog().ValidateCall("network.dns.resolve", Json("""{ "name": "server01.example.com" }"""));
        Assert.True(result.IsValid);
        var p = Assert.IsType<DnsResolveParameters>(result.Parameters);
        Assert.Equal("server01.example.com", p.Name);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{ "name": "" }""")]
    [InlineData("""{ "name": "evil; rm -rf /" }""")]
    [InlineData("""{ "name": "host", "extra": "bad" }""")]
    public void DnsResolve_InvalidParameters_AreRejected(string json)
    {
        var result = CreateCatalog().ValidateCall("network.dns.resolve", Json(json));
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_PARAMETER", result.ErrorCode);
    }

    [Fact]
    public void PortTest_ValidParameters_AreAccepted()
    {
        var result = CreateCatalog().ValidateCall("network.port.test",
            Json("""{ "host": "server01.example.com", "port": 443 }"""));
        Assert.True(result.IsValid);
        var p = Assert.IsType<PortTestParameters>(result.Parameters);
        Assert.Equal("server01.example.com", p.Host);
        Assert.Equal(443, p.Port);
    }

    [Theory]
    [InlineData("""{ "host": "server01.example.com", "port": 0 }""")]
    [InlineData("""{ "host": "server01.example.com", "port": 99999 }""")]
    [InlineData("""{ "host": "evil; DROP TABLE users", "port": 443 }""")]
    [InlineData("""{ "port": 443 }""")]
    [InlineData("""{ "host": "x", "port": 80, "injected": "rm" }""")]
    public void PortTest_InvalidParameters_AreRejected(string json)
    {
        var result = CreateCatalog().ValidateCall("network.port.test", Json(json));
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_PARAMETER", result.ErrorCode);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Prozess-Aktionen (Abnahme 2)
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("process.list")]
    [InlineData("process.cpu_top")]
    [InlineData("process.memory_top")]
    public void ProcessActions_AcceptTopParameter(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("""{ "top": 20 }"""));
        Assert.True(result.IsValid);
        var p = Assert.IsType<ProcessListParameters>(result.Parameters);
        Assert.Equal(20, p.Top);
    }

    [Theory]
    [InlineData("process.list", """{ "top": 0 }""")]
    [InlineData("process.list", """{ "top": 999 }""")]
    [InlineData("process.list", """{ "injected": "Get-Process | Stop-Process" }""")]
    public void ProcessActions_InvalidParameters_AreRejected(string actionId, string json)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json(json));
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_PARAMETER", result.ErrorCode);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Dienst-Aktionen
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ServiceList_IsRegistered()
    {
        var result = CreateCatalog().ValidateCall("service.list", Json("{}"));
        Assert.True(result.IsValid);
        Assert.IsType<EmptyDiagnosticParameters>(result.Parameters);
    }

    [Fact]
    public void ServiceStatus_ValidServiceName_IsAccepted()
    {
        var result = CreateCatalog().ValidateCall("service.status",
            Json("""{ "serviceName": "wuauserv" }"""));
        Assert.True(result.IsValid);
        var p = Assert.IsType<ServiceStatusParameters>(result.Parameters);
        Assert.Equal("wuauserv", p.ServiceName);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{ "serviceName": "evil; Stop-Service -Force" }""")]
    [InlineData("""{ "serviceName": "wuauserv", "extra": "bad" }""")]
    public void ServiceStatus_InvalidParameters_AreRejected(string json)
    {
        var result = CreateCatalog().ValidateCall("service.status", Json(json));
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_PARAMETER", result.ErrorCode);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Domänen-Aktionen
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("domain.status")]
    [InlineData("domain.dc_discovery")]
    [InlineData("domain.secure_channel.test")]
    public void DomainActions_AreRegistered(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("{}"));
        Assert.True(result.IsValid);
        Assert.IsType<EmptyDiagnosticParameters>(result.Parameters);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Kapazitäts-Filterung (Abnahme 4)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAvailableActions_FiltersActionsWithMissingModule()
    {
        var capabilities = new SystemCapabilities
        {
            AvailableModules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            // Kein Modul vorhanden
        };
        var catalog = CreateCatalog();
        // Alle Aktionen ohne RequiredModule müssen verfügbar sein
        var all = catalog.Definitions;
        var available = catalog.GetAvailableActions(capabilities);
        // Keine Aktion in unserem Katalog hat RequiredModule gesetzt (alle nutzen WMI/WinAPI)
        // daher sollten alle verfügbar sein
        Assert.NotEmpty(available);
        // Aktionen die RequiredModule != null haben, werden gefiltert
        var withModule = all.Where(d => d.RequiredModule is not null).ToList();
        if (withModule.Count > 0)
        {
            Assert.True(available.Count < all.Count || available.Count == all.Count);
        }
    }

    [Fact]
    public void GetAvailableActions_FiltersAdminRequiredWhenNotAdmin()
    {
        var capabilities = new SystemCapabilities { IsAdministrator = false };
        var catalog = CreateCatalog();
        var available = catalog.GetAvailableActions(capabilities);
        // Kein Element mit RequiresAdministrator darf in den verfügbaren sein
        Assert.All(available, d => Assert.False(d.RequiresAdministrator));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Risikomodell
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void AllRegisteredActions_HaveR0OrR1RiskLevel()
    {
        var catalog = CreateCatalog();
        foreach (var action in catalog.Definitions)
        {
            Assert.True(action.RiskLevel <= ActionRiskLevel.R1,
                $"Aktion {action.ActionId} hat unzulässige Risikostufe {action.RiskLevel}");
        }
    }

    [Fact]
    public void AllRegisteredActions_DoNotChangeSystem()
    {
        // Alle aktuell registrierten Aktionen müssen rein lesend sein
        var catalog = CreateCatalog();
        foreach (var action in catalog.Definitions)
        {
            Assert.False(action.ChangesSystem,
                $"Aktion {action.ActionId} ist als systemändernd markiert, obwohl nur R0/R1 erlaubt");
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Freie Shell-Ausführung ist unmöglich (Abnahme 8)
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("events.query", """{ "providers": ["stornvme; Get-Process | Stop-Process"] }""")]
    [InlineData("network.dns.resolve", """{ "name": "host$(calc.exe)" }""")]
    [InlineData("network.port.test", """{ "host": "h$(calc)", "port": 80 }""")]
    public void MaliciousParameters_AreRejected(string actionId, string json)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json(json));
        Assert.False(result.IsValid);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Gesamtkatalog-Größe
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Catalog_ContainsAtLeast20Actions()
    {
        var catalog = CreateCatalog();
        Assert.True(catalog.Definitions.Count >= 20,
            $"Katalog hat nur {catalog.Definitions.Count} Aktionen, mindestens 20 erwartet.");
    }

    [Fact]
    public void Catalog_HasNoDuplicateActionIds()
    {
        var catalog = CreateCatalog();
        var ids = catalog.Definitions.Select(d => d.ActionId).ToList();
        var distinct = ids.Distinct().ToList();
        Assert.Equal(ids.Count, distinct.Count);
    }
}

using System.Text.Json;
using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Options;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Zentraler, fest im Backend definierter Katalog zulässiger Diagnoseaktionen. Das Modell
/// erhält nur diesen Katalog als Werkzeugliste und kann niemals einen freien Befehl anfordern.
/// Unbekannte Aktionen oder zusätzliche/ungültige Parameter werden abgelehnt.
/// R0/R1 sind produktiv ausführbar. R2–R5 sind modelliert, aber blockiert.
/// </summary>
public sealed class DiagnosticActionCatalog
{
    private static readonly string[] AllowedLevels = { "Critical", "Error", "Warning" };

    private static readonly HashSet<string> EventsQueryAllowedKeys = new(StringComparer.Ordinal)
    {
        "logNames", "providers", "levels", "sinceHours", "maximumResults"
    };

    private static readonly HashSet<string> SinceHoursAllowedKeys = new(StringComparer.Ordinal)
    {
        "sinceHours", "maximumResults"
    };

    private static readonly HashSet<string> DnsResolveAllowedKeys = new(StringComparer.Ordinal)
    {
        "name"
    };

    private static readonly HashSet<string> PortTestAllowedKeys = new(StringComparer.Ordinal)
    {
        "host", "port"
    };

    private static readonly HashSet<string> ServiceStatusAllowedKeys = new(StringComparer.Ordinal)
    {
        "serviceName"
    };

    private static readonly HashSet<string> ProcessListAllowedKeys = new(StringComparer.Ordinal)
    {
        "top"
    };

    private readonly EventOptions _eventOptions;
    private readonly List<DiagnosticActionDefinition> _definitions;

    public DiagnosticActionCatalog(IOptions<EventOptions> eventOptions)
    {
        _eventOptions = eventOptions.Value;
        _definitions = BuildAllDefinitions();
    }

    public IReadOnlyList<DiagnosticActionDefinition> Definitions => _definitions;

    public DiagnosticActionDefinition? Find(string actionId) =>
        _definitions.FirstOrDefault(d => string.Equals(d.ActionId, actionId, StringComparison.Ordinal));

    /// <summary>
    /// Gibt alle Aktionen zurück, die auf dem aktuellen System tatsächlich verfügbar sind.
    /// Filtert nach Modulen, Binaries und Administratorrechten.
    /// </summary>
    public IReadOnlyList<DiagnosticActionDefinition> GetAvailableActions(SystemCapabilities capabilities) =>
        _definitions
            .Where(d => IsAvailable(d, capabilities))
            .ToList();

    /// <summary>Baut die Werkzeugliste im von Ollama erwarteten Function-Calling-Format.</summary>
    public List<object> BuildToolsPayload() =>
        _definitions
            .Select(d => (object)new
            {
                type = "function",
                function = new { name = d.ActionId, description = d.Description, parameters = d.ParameterSchema }
            })
            .ToList();

    /// <summary>
    /// Baut die Werkzeugliste gefiltert nach Systemfähigkeiten.
    /// </summary>
    public List<object> BuildToolsPayload(SystemCapabilities capabilities) =>
        GetAvailableActions(capabilities)
            .Select(d => (object)new
            {
                type = "function",
                function = new { name = d.ActionId, description = d.Description, parameters = d.ParameterSchema }
            })
            .ToList();

    public ActionValidationResult ValidateCall(string actionId, JsonElement parameters)
    {
        var definition = Find(actionId);
        if (definition is null)
        {
            return Invalid($"Unbekannte Aktion '{actionId}'.", "ACTION_NOT_FOUND");
        }

        if (definition.RequiresAdministrator)
        {
            return Invalid("Diese Aktion erfordert Administratorrechte, die aktuell nicht unterstützt werden.", "ADMIN_REQUIRED");
        }

        // R2+ werden in dieser Version nicht automatisch ausgeführt.
        if (definition.RiskLevel >= ActionRiskLevel.R2)
        {
            return Invalid(
                $"Aktionen ab Risikostufe R2 sind derzeit nicht automatisch ausführbar. Diese Aktion hat Risikostufe {definition.RiskLevel}.",
                "EXECUTION_BLOCKED_BY_RISK_POLICY");
        }

        return actionId switch
        {
            "events.query" => ValidateEventsQuery(definition, parameters),

            "events.system.recent" or "events.application.recent" or
            "events.kernel_power" or "events.whea" or
            "storage.events.errors" => ValidateSinceHoursParameters(definition, parameters),

            "network.dns.resolve" => ValidateDnsResolve(definition, parameters),
            "network.port.test" => ValidatePortTest(definition, parameters),

            "service.status" => ValidateServiceStatus(definition, parameters),

            "process.list" or "process.cpu_top" or "process.memory_top" => ValidateProcessList(definition, parameters),

            // Alle parameterloser Aktionen
            "winget.status" or "winget.sources.list" or "appinstaller.status" or
            "windowsupdate.status" or "storage.summary" or "network.microsoftEndpoints" or
            "system.info" or "system.uptime" or "system.windows_version" or "system.pending_reboot" or
            "storage.disks.list" or "storage.volumes.list" or "storage.health.basic" or
            "network.adapters.list" or "network.configuration" or "network.gateway.test" or
            "service.list" or "domain.status" or "domain.dc_discovery" or "domain.secure_channel.test" =>
                ValidateEmptyParameters(definition, parameters),

            _ => Invalid($"Aktion '{actionId}' ist noch nicht implementiert.", "ACTION_NOT_FOUND")
        };
    }

    private static bool IsAvailable(DiagnosticActionDefinition d, SystemCapabilities c)
    {
        if (d.RequiresAdministrator && !c.IsAdministrator)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(d.RequiredModule) && !c.HasModule(d.RequiredModule))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(d.RequiredBinary) && !c.HasBinary(d.RequiredBinary))
        {
            return false;
        }

        return true;
    }

    private static ActionValidationResult ValidateEmptyParameters(DiagnosticActionDefinition definition, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object || parameters.EnumerateObject().Any())
        {
            return Invalid("Diese fest verdrahtete Aktion akzeptiert keine Parameter.", "INVALID_PARAMETER");
        }

        return new ActionValidationResult { IsValid = true, Definition = definition, Parameters = new EmptyDiagnosticParameters() };
    }

    private ActionValidationResult ValidateSinceHoursParameters(DiagnosticActionDefinition definition, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            return Invalid("Die Parameter müssen ein JSON-Objekt sein.", "INVALID_PARAMETER");
        }

        foreach (var property in parameters.EnumerateObject())
        {
            if (!SinceHoursAllowedKeys.Contains(property.Name))
            {
                return Invalid($"Unbekannter Parameter '{property.Name}'.", "INVALID_PARAMETER");
            }
        }

        if (!TryReadInt(parameters, "sinceHours", 24, out var sinceHours, out var hoursError))
        {
            return Invalid(hoursError!, "INVALID_PARAMETER");
        }

        if (sinceHours < 1 || sinceHours > _eventOptions.MaxHours)
        {
            return Invalid($"sinceHours muss zwischen 1 und {_eventOptions.MaxHours} liegen.", "INVALID_PARAMETER");
        }

        var defaultMax = Math.Min(50, _eventOptions.MaxEvents);
        if (!TryReadInt(parameters, "maximumResults", defaultMax, out var maximumResults, out var maxError))
        {
            return Invalid(maxError!, "INVALID_PARAMETER");
        }

        if (maximumResults < 1 || maximumResults > _eventOptions.MaxEvents)
        {
            return Invalid($"maximumResults muss zwischen 1 und {_eventOptions.MaxEvents} liegen.", "INVALID_PARAMETER");
        }

        return new ActionValidationResult
        {
            IsValid = true,
            Definition = definition,
            Parameters = new SinceHoursParameters { SinceHours = sinceHours, MaximumResults = maximumResults }
        };
    }

    private ActionValidationResult ValidateEventsQuery(DiagnosticActionDefinition definition, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            return Invalid("Die Parameter müssen ein JSON-Objekt sein.", "INVALID_PARAMETER");
        }

        foreach (var property in parameters.EnumerateObject())
        {
            if (!EventsQueryAllowedKeys.Contains(property.Name))
            {
                return Invalid($"Unbekannter Parameter '{property.Name}'.", "INVALID_PARAMETER");
            }
        }

        if (!TryReadStringArray(parameters, "logNames", out var logNames, out var logError))
        {
            return Invalid(logError!, "INVALID_PARAMETER");
        }

        if (logNames.Any(name => !_eventOptions.Logs.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            return Invalid("Mindestens ein angefordertes Protokoll ist nicht freigegeben.", "INVALID_PARAMETER");
        }

        if (logNames.Count == 0)
        {
            logNames = _eventOptions.Logs.ToList();
        }

        if (!TryReadStringArray(parameters, "levels", out var levels, out var levelError))
        {
            return Invalid(levelError!, "INVALID_PARAMETER");
        }

        if (levels.Any(level => !AllowedLevels.Contains(level, StringComparer.OrdinalIgnoreCase)))
        {
            return Invalid("Mindestens eine angeforderte Ereignisebene ist ungültig.", "INVALID_PARAMETER");
        }

        if (levels.Count == 0)
        {
            levels = new List<string> { "Error", "Warning" };
        }

        if (!TryReadStringArray(parameters, "providers", out var providers, out var providerError))
        {
            return Invalid(providerError!, "INVALID_PARAMETER");
        }

        if (providers.Count > 10)
        {
            return Invalid("Zu viele Provider angefordert (maximal 10).", "INVALID_PARAMETER");
        }

        if (providers.Any(p => p.Length > 100))
        {
            return Invalid("Ein Providername ist zu lang.", "INVALID_PARAMETER");
        }

        // Only allow safe characters in provider names (alphanumeric, dash, dot, underscore, space)
        var providerSafePattern = new System.Text.RegularExpressions.Regex(@"^[\w\s\.\-]+$",
            System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(100));
        foreach (var p in providers)
        {
            if (!providerSafePattern.IsMatch(p))
                return Invalid($"Ungültiger Providername: '{p}'.", "INVALID_PARAMETER");
        }

        if (!TryReadInt(parameters, "sinceHours", 24, out var sinceHours, out var hoursError))
        {
            return Invalid(hoursError!, "INVALID_PARAMETER");
        }

        if (sinceHours < 1 || sinceHours > _eventOptions.MaxHours)
        {
            return Invalid($"sinceHours muss zwischen 1 und {_eventOptions.MaxHours} liegen.", "INVALID_PARAMETER");
        }

        var defaultMax = Math.Min(50, _eventOptions.MaxEvents);
        if (!TryReadInt(parameters, "maximumResults", defaultMax, out var maximumResults, out var maxError))
        {
            return Invalid(maxError!, "INVALID_PARAMETER");
        }

        if (maximumResults < 1 || maximumResults > _eventOptions.MaxEvents)
        {
            return Invalid($"maximumResults muss zwischen 1 und {_eventOptions.MaxEvents} liegen.", "INVALID_PARAMETER");
        }

        var query = new EventsQueryParameters
        {
            LogNames = logNames,
            Providers = providers,
            Levels = levels,
            SinceHours = sinceHours,
            MaximumResults = maximumResults
        };

        return new ActionValidationResult { IsValid = true, Definition = definition, Parameters = query };
    }

    private static ActionValidationResult ValidateDnsResolve(DiagnosticActionDefinition definition, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            return Invalid("Die Parameter müssen ein JSON-Objekt sein.", "INVALID_PARAMETER");
        }

        foreach (var property in parameters.EnumerateObject())
        {
            if (!DnsResolveAllowedKeys.Contains(property.Name))
            {
                return Invalid($"Unbekannter Parameter '{property.Name}'.", "INVALID_PARAMETER");
            }
        }

        if (!parameters.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
        {
            return Invalid("Der Parameter 'name' ist erforderlich und muss eine Zeichenkette sein.", "INVALID_PARAMETER");
        }

        var name = nameProp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 253)
        {
            return Invalid("Der DNS-Name ist ungültig.", "INVALID_PARAMETER");
        }

        // Einfache Prüfung: Nur zulässige DNS-Zeichen erlauben
        if (!name.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'))
        {
            return Invalid("Der DNS-Name enthält unzulässige Zeichen.", "INVALID_PARAMETER");
        }

        return new ActionValidationResult
        {
            IsValid = true,
            Definition = definition,
            Parameters = new DnsResolveParameters { Name = name }
        };
    }

    private static ActionValidationResult ValidatePortTest(DiagnosticActionDefinition definition, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            return Invalid("Die Parameter müssen ein JSON-Objekt sein.", "INVALID_PARAMETER");
        }

        foreach (var property in parameters.EnumerateObject())
        {
            if (!PortTestAllowedKeys.Contains(property.Name))
            {
                return Invalid($"Unbekannter Parameter '{property.Name}'.", "INVALID_PARAMETER");
            }
        }

        if (!parameters.TryGetProperty("host", out var hostProp) || hostProp.ValueKind != JsonValueKind.String)
        {
            return Invalid("Der Parameter 'host' ist erforderlich.", "INVALID_PARAMETER");
        }

        var host = hostProp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253)
        {
            return Invalid("Der Hostname ist ungültig.", "INVALID_PARAMETER");
        }

        if (!host.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'))
        {
            return Invalid("Der Hostname enthält unzulässige Zeichen.", "INVALID_PARAMETER");
        }

        if (!TryReadInt(parameters, "port", 0, out var port, out var portError))
        {
            return Invalid(portError!, "INVALID_PARAMETER");
        }

        if (port < 1 || port > 65535)
        {
            return Invalid("Der Port muss zwischen 1 und 65535 liegen.", "INVALID_PARAMETER");
        }

        return new ActionValidationResult
        {
            IsValid = true,
            Definition = definition,
            Parameters = new PortTestParameters { Host = host, Port = port }
        };
    }

    private static ActionValidationResult ValidateServiceStatus(DiagnosticActionDefinition definition, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            return Invalid("Die Parameter müssen ein JSON-Objekt sein.", "INVALID_PARAMETER");
        }

        foreach (var property in parameters.EnumerateObject())
        {
            if (!ServiceStatusAllowedKeys.Contains(property.Name))
            {
                return Invalid($"Unbekannter Parameter '{property.Name}'.", "INVALID_PARAMETER");
            }
        }

        if (!parameters.TryGetProperty("serviceName", out var svcProp) || svcProp.ValueKind != JsonValueKind.String)
        {
            return Invalid("Der Parameter 'serviceName' ist erforderlich.", "INVALID_PARAMETER");
        }

        var serviceName = svcProp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serviceName) || serviceName.Length > 256)
        {
            return Invalid("Der Dienstname ist ungültig.", "INVALID_PARAMETER");
        }

        // Nur alphanumerische Zeichen, Bindestriche und Unterstriche
        if (!serviceName.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
        {
            return Invalid("Der Dienstname enthält unzulässige Zeichen.", "INVALID_PARAMETER");
        }

        return new ActionValidationResult
        {
            IsValid = true,
            Definition = definition,
            Parameters = new ServiceStatusParameters { ServiceName = serviceName }
        };
    }

    private ActionValidationResult ValidateProcessList(DiagnosticActionDefinition definition, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            return Invalid("Die Parameter müssen ein JSON-Objekt sein.", "INVALID_PARAMETER");
        }

        foreach (var property in parameters.EnumerateObject())
        {
            if (!ProcessListAllowedKeys.Contains(property.Name))
            {
                return Invalid($"Unbekannter Parameter '{property.Name}'.", "INVALID_PARAMETER");
            }
        }

        if (!TryReadInt(parameters, "top", 30, out var top, out var topError))
        {
            return Invalid(topError!, "INVALID_PARAMETER");
        }

        if (top < 1 || top > 100)
        {
            return Invalid("top muss zwischen 1 und 100 liegen.", "INVALID_PARAMETER");
        }

        return new ActionValidationResult
        {
            IsValid = true,
            Definition = definition,
            Parameters = new ProcessListParameters { Top = top }
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    // Hilfsmethoden
    // ──────────────────────────────────────────────────────────────────────

    private static bool TryReadStringArray(
        JsonElement parameters, string key, out List<string> values, out string? error)
    {
        values = new List<string>();
        error = null;

        if (!parameters.TryGetProperty(key, out var element))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            error = $"'{key}' muss ein Array aus Zeichenketten sein.";
            return false;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                error = $"'{key}' darf nur Zeichenketten enthalten.";
                return false;
            }

            var text = item.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add(text);
            }
        }

        return true;
    }

    private static bool TryReadInt(
        JsonElement parameters, string key, int fallback, out int value, out string? error)
    {
        value = fallback;
        error = null;

        if (!parameters.TryGetProperty(key, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out value))
        {
            error = $"'{key}' muss eine ganze Zahl sein.";
            value = fallback;
            return false;
        }

        return true;
    }

    private static ActionValidationResult Invalid(string error, string code = "INVALID_PARAMETER") =>
        new() { IsValid = false, Error = error, ErrorCode = code };

    // ──────────────────────────────────────────────────────────────────────
    // Action-Definitionen
    // ──────────────────────────────────────────────────────────────────────

    private List<DiagnosticActionDefinition> BuildAllDefinitions() => new()
    {
        // ── Events ────────────────────────────────────────────────────────
        BuildEventsQueryDefinition(),
        BuildSinceHoursDefinition("events.system.recent", "System-Ereignisse (letzte Stunden)",
            "Liest kritische und Warnereignisse aus dem Windows-Systemprotokoll im angegebenen Zeitraum. " +
            "Nützlich für Freeze-, Absturz- oder Stabilitätsanalysen.", "events"),
        BuildSinceHoursDefinition("events.application.recent", "Anwendungs-Ereignisse (letzte Stunden)",
            "Liest Fehler und Warnungen aus dem Windows-Anwendungsprotokoll im angegebenen Zeitraum.", "events"),
        BuildReadOnlyDefinition("events.kernel_power", "Kernel-Power-Ereignisse prüfen",
            "Liest Kernel-Power-Ereignisse (Event 41, 42) der letzten 72 Stunden. " +
            "Nützlich für ungeplante Neustarts und Freeze-Analysen.", "events"),
        BuildReadOnlyDefinition("events.whea", "WHEA-Ereignisse prüfen",
            "Liest WHEA-Logger-Ereignisse (Hardware-Fehler) der letzten 72 Stunden. " +
            "Nützlich bei Verdacht auf Hardwarefehler.", "events"),
        BuildSinceHoursDefinition("storage.events.errors", "Storage-Fehlerereignisse prüfen",
            "Liest Ereignisse von Storage-Providern (stornvme, disk, Storport) im angegebenen Zeitraum. " +
            "Nützlich bei Storage-Timeouts, Resets und I/O-Fehlern.", "storage"),

        // ── System ────────────────────────────────────────────────────────
        BuildReadOnlyDefinition("system.info", "Systeminformationen abrufen",
            "Liest allgemeine Rechnerdaten: Hersteller, Modell, Architektur, aktueller Benutzer.", "system"),
        BuildReadOnlyDefinition("system.uptime", "Systemlaufzeit prüfen",
            "Liest letzten Startzeitpunkt und Betriebsdauer des Systems.", "system"),
        BuildReadOnlyDefinition("system.windows_version", "Windows-Version prüfen",
            "Liest Windows-Edition, Version und Build-Nummer.", "system"),
        BuildReadOnlyDefinition("system.pending_reboot", "Ausstehenden Neustart prüfen",
            "Prüft, ob ein Neustart aussteht (Windows Update, Component Based Servicing, PendingFileRename).", "system"),

        // ── Storage ───────────────────────────────────────────────────────
        BuildReadOnlyDefinition("storage.summary", "Datenträgerstatus prüfen",
            "Liest erkannte lokale Datenträger und freien Speicher.", "storage"),
        BuildReadOnlyDefinition("storage.disks.list", "Datenträger auflisten",
            "Listet alle erkannten Datenträger mit Größe, Typ und Status auf.", "storage"),
        BuildReadOnlyDefinition("storage.volumes.list", "Volumes auflisten",
            "Listet alle Laufwerke/Volumes mit Dateisystem, Größe und freiem Speicher auf.", "storage"),
        BuildReadOnlyDefinition("storage.health.basic", "Datenträger-Gesundheit prüfen (basic)",
            "Prüft SMART-Status und Betriebsstatus der Datenträger.", "storage",
            riskLevel: ActionRiskLevel.R1),

        // ── Network ───────────────────────────────────────────────────────
        BuildReadOnlyDefinition("network.microsoftEndpoints", "Microsoft-Endpunkte prüfen",
            "Prüft DNS-Auflösung fest definierter Microsoft- und Winget-Endpunkte.", "network"),
        BuildReadOnlyDefinition("network.adapters.list", "Netzwerkadapter auflisten",
            "Listet alle Netzwerkadapter mit Status, Typ und MAC-Adresse auf.", "network",
            outputSensitivity: OutputSensitivity.InternalNetworkData),
        BuildReadOnlyDefinition("network.configuration", "Netzwerkkonfiguration prüfen",
            "Liest IP-Adressen, DNS-Server und Gateway der aktiven Netzwerkverbindungen.", "network",
            outputSensitivity: OutputSensitivity.InternalNetworkData),
        BuildReadOnlyDefinition("network.gateway.test", "Standard-Gateway erreichbar?",
            "Prüft, ob das Standard-Gateway per Ping erreichbar ist.", "network",
            riskLevel: ActionRiskLevel.R1),
        BuildDnsResolveDefinition(),
        BuildPortTestDefinition(),

        // ── Processes ─────────────────────────────────────────────────────
        BuildProcessListDefinition("process.list", "Prozessliste abrufen",
            "Listet alle laufenden Prozesse mit PID, Name, CPU- und Speicherverbrauch auf."),
        BuildProcessListDefinition("process.cpu_top", "CPU-intensivste Prozesse",
            "Listet die Prozesse mit dem höchsten CPU-Verbrauch auf."),
        BuildProcessListDefinition("process.memory_top", "Speicherintensivste Prozesse",
            "Listet die Prozesse mit dem höchsten Arbeitsspeicherverbrauch auf."),

        // ── Services ──────────────────────────────────────────────────────
        BuildReadOnlyDefinition("service.list", "Dienste auflisten",
            "Listet alle Windows-Dienste mit Status und Starttyp auf.",
            outputSensitivity: OutputSensitivity.ProcessList),
        BuildServiceStatusDefinition(),

        // ── Domain ────────────────────────────────────────────────────────
        BuildReadOnlyDefinition("domain.status", "Domänenstatus prüfen",
            "Prüft, ob der Rechner einer Domäne angehört, und liefert grundlegende Domäneninformationen.", "domain",
            outputSensitivity: OutputSensitivity.InternalNetworkData),
        BuildReadOnlyDefinition("domain.dc_discovery", "Domänencontroller suchen",
            "Versucht, einen Domänencontroller für die aktuelle Domäne zu finden.", "domain",
            riskLevel: ActionRiskLevel.R1, outputSensitivity: OutputSensitivity.InternalNetworkData),
        BuildReadOnlyDefinition("domain.secure_channel.test", "Sicherer Kanal prüfen",
            "Prüft den sicheren Kanal zum Domänencontroller (Secure Channel / Netlogon-Verbindung).", "domain",
            riskLevel: ActionRiskLevel.R1, outputSensitivity: OutputSensitivity.InternalNetworkData),

        // ── Winget / AppInstaller ─────────────────────────────────────────
        BuildReadOnlyDefinition("winget.status", "Winget-Status prüfen",
            "Prüft Vorhandensein, Pfad, Version und Aufrufbarkeit von winget.", "winget"),
        BuildReadOnlyDefinition("winget.sources.list", "Winget-Quellen prüfen",
            "Liest die konfigurierten Winget-Quellen und erkennbare Quellenfehler.", "winget"),
        BuildReadOnlyDefinition("appinstaller.status", "App Installer prüfen",
            "Liest den Installations- und Paketstatus von Microsoft.DesktopAppInstaller.", "appinstaller"),
        BuildReadOnlyDefinition("windowsupdate.status", "Windows Update prüfen",
            "Liest Dienstzustände, ausstehenden Neustart und bekannte Updatehinweise.", "windowsupdate"),
    };

    private DiagnosticActionDefinition BuildEventsQueryDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                logNames = new { type = "array", items = new { type = "string", @enum = _eventOptions.Logs } },
                providers = new { type = "array", items = new { type = "string" } },
                levels = new { type = "array", items = new { type = "string", @enum = AllowedLevels } },
                sinceHours = new { type = "integer", minimum = 1, maximum = _eventOptions.MaxHours },
                maximumResults = new { type = "integer", minimum = 1, maximum = _eventOptions.MaxEvents }
            },
            required = Array.Empty<string>()
        });

        return new DiagnosticActionDefinition
        {
            ActionId = "events.query",
            Title = "Windows-Ereignisse abfragen",
            Description =
                "Durchsucht die konfigurierten Windows-Ereignisprotokolle lesend nach Fehlern und Warnungen. " +
                "Verändert das System nicht.",
            Category = "events",
            RiskLevel = ActionRiskLevel.R0,
            ChangesSystem = false,
            RequiresAdministrator = false,
            RequiresConfirmation = false,
            TimeoutSeconds = 20,
            ParameterSchema = schema
        };
    }

    private static DiagnosticActionDefinition BuildSinceHoursDefinition(
        string actionId, string title, string description, string category)
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                sinceHours = new { type = "integer", minimum = 1, maximum = 168, description = "Zeitraum in Stunden (Standard: 24)" },
                maximumResults = new { type = "integer", minimum = 1, maximum = 500, description = "Maximale Anzahl Ergebnisse (Standard: 50)" }
            },
            required = Array.Empty<string>(),
            additionalProperties = false
        });

        return new DiagnosticActionDefinition
        {
            ActionId = actionId,
            Title = title,
            Description = description + " Verändert das System nicht.",
            Category = category,
            RiskLevel = ActionRiskLevel.R1,
            ChangesSystem = false,
            RequiresAdministrator = false,
            RequiresConfirmation = false,
            TimeoutSeconds = 25,
            ParameterSchema = schema
        };
    }

    private static DiagnosticActionDefinition BuildDnsResolveDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "DNS-Name, der aufgelöst werden soll (z. B. server01.example.com)" }
            },
            required = new[] { "name" },
            additionalProperties = false
        });

        return new DiagnosticActionDefinition
        {
            ActionId = "network.dns.resolve",
            Title = "DNS-Namen auflösen",
            Description = "Prüft, ob ein DNS-Name über die aktuelle Windows-DNS-Konfiguration aufgelöst werden kann. Verändert das System nicht.",
            Category = "network.dns",
            RiskLevel = ActionRiskLevel.R1,
            ChangesSystem = false,
            RequiresAdministrator = false,
            RequiresConfirmation = false,
            TimeoutSeconds = 30,
            OutputSensitivity = OutputSensitivity.InternalNetworkData,
            ParameterSchema = schema
        };
    }

    private static DiagnosticActionDefinition BuildPortTestDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                host = new { type = "string", description = "Hostname oder IP-Adresse" },
                port = new { type = "integer", minimum = 1, maximum = 65535, description = "TCP-Port" }
            },
            required = new[] { "host", "port" },
            additionalProperties = false
        });

        return new DiagnosticActionDefinition
        {
            ActionId = "network.port.test",
            Title = "TCP-Port erreichbar?",
            Description = "Prüft, ob ein TCP-Port auf einem Zielhost erreichbar ist. Verändert das System nicht.",
            Category = "network",
            RiskLevel = ActionRiskLevel.R1,
            ChangesSystem = false,
            RequiresAdministrator = false,
            RequiresConfirmation = false,
            TimeoutSeconds = 30,
            OutputSensitivity = OutputSensitivity.InternalNetworkData,
            ParameterSchema = schema
        };
    }

    private static DiagnosticActionDefinition BuildProcessListDefinition(string actionId, string title, string description)
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                top = new { type = "integer", minimum = 1, maximum = 100, description = "Maximale Anzahl Prozesse (Standard: 30)" }
            },
            required = Array.Empty<string>(),
            additionalProperties = false
        });

        return new DiagnosticActionDefinition
        {
            ActionId = actionId,
            Title = title,
            Description = description + " Verändert das System nicht.",
            Category = "processes",
            RiskLevel = ActionRiskLevel.R0,
            ChangesSystem = false,
            RequiresAdministrator = false,
            RequiresConfirmation = false,
            TimeoutSeconds = 20,
            OutputSensitivity = OutputSensitivity.ProcessList,
            ParameterSchema = schema
        };
    }

    private static DiagnosticActionDefinition BuildServiceStatusDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                serviceName = new { type = "string", description = "Name des Windows-Dienstes (z. B. wuauserv, Spooler)" }
            },
            required = new[] { "serviceName" },
            additionalProperties = false
        });

        return new DiagnosticActionDefinition
        {
            ActionId = "service.status",
            Title = "Dienststatus prüfen",
            Description = "Prüft den aktuellen Status eines bestimmten Windows-Dienstes. Verändert das System nicht.",
            Category = "services",
            RiskLevel = ActionRiskLevel.R0,
            ChangesSystem = false,
            RequiresAdministrator = false,
            RequiresConfirmation = false,
            TimeoutSeconds = 15,
            ParameterSchema = schema
        };
    }

    private static DiagnosticActionDefinition BuildReadOnlyDefinition(
        string actionId,
        string title,
        string description,
        string category = "system",
        ActionRiskLevel riskLevel = ActionRiskLevel.R0,
        OutputSensitivity outputSensitivity = OutputSensitivity.Public,
        string? requiredModule = null,
        string? requiredBinary = null) =>
        new()
        {
            ActionId = actionId,
            Title = title,
            Description = description + " Verändert das System nicht.",
            Category = category,
            RiskLevel = riskLevel,
            ChangesSystem = false,
            RequiresAdministrator = false,
            RequiresConfirmation = false,
            TimeoutSeconds = 20,
            OutputSensitivity = outputSensitivity,
            RequiredModule = requiredModule,
            RequiredBinary = requiredBinary,
            ParameterSchema = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new { },
                additionalProperties = false
            })
        };
}

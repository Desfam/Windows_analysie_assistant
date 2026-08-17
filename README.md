# Windows Diagnose Assistent

Eine lokale Windows-Diagnoseanwendung als Web-App. Sie wird als Windows-EXE gestartet,
startet im Hintergrund einen lokalen Webserver und öffnet die Benutzeroberfläche
automatisch im Standardbrowser. Angezeigt werden grundlegende Rechnerinformationen und
relevante Windows-Ereignisse.

Die Anwendung ist erreichbar unter: **http://127.0.0.1:5187**

Der Webserver wird **ausschließlich an `127.0.0.1`** gebunden und ist nicht aus dem
Netzwerk erreichbar.

---

## Inhaltsverzeichnis

- [Zweck der Anwendung](#zweck-der-anwendung)
- [Aktueller Funktionsumfang](#aktueller-funktionsumfang)
- [Bewusst noch nicht enthaltene Funktionen](#bewusst-noch-nicht-enthaltene-funktionen)
- [Voraussetzungen für die Entwicklung](#voraussetzungen-für-die-entwicklung)
- [Installation](#installation)
- [Start im Entwicklungsmodus](#start-im-entwicklungsmodus)
- [Vollständiger Build und Windows-EXE](#vollständiger-build-und-windows-exe)
- [Speicherort des veröffentlichten Programms](#speicherort-des-veröffentlichten-programms)
- [Benötigte Windows-Berechtigungen](#benötigte-windows-berechtigungen)
- [Lokale Netzwerkbindung](#lokale-netzwerkbindung)
- [Sitzungstoken](#sitzungstoken)
- [API-Endpunkte](#api-endpunkte)
- [Bekannte Einschränkungen](#bekannte-einschränkungen)
- [Fehlerbehebung](#fehlerbehebung)
- [Projektstruktur](#projektstruktur)
- [Tests](#tests)

---

## Zweck der Anwendung

Der Windows Diagnose Assistent liest lokal und **ausschließlich lesend** grundlegende
Informationen über den Rechner aus und bereitet relevante Windows-Ereignisse
verständlich auf. Er richtet sich an Personen, die schnell einen ruhigen, modernen
Überblick über den Systemzustand und die wichtigsten Ereignisse der letzten Stunden
erhalten möchten – ohne die klassische Windows-Ereignisanzeige durchsuchen zu müssen.

## Aktueller Funktionsumfang

- Übersichtliche, moderne, dunkle Hauptseite
- **Obere Statusleiste** mit Anwendungsname, Rechnername, Zeitpunkt der letzten
  Aktualisierung, Erfassungszustand, Schaltfläche „Aktualisieren“, Einstellungen und
  Statusanzeige „Lokale Verbindung“
- **Linke Rechnerübersicht** (ca. 25 % Breite) mit aufklappbaren Bereichen:
  Allgemein, CPU, Arbeitsspeicher, GPU (alle Grafikkarten), Datenträger, Windows
  – jeweils mit Statussymbol (Normal / Warnung / Kritisch / Nicht geprüft)
- **Große Ereignisübersicht** (ca. 75 % Breite) als Karten statt Tabelle
- Verständliche Erklärungen für bekannte, wichtige Ereignisse
- Gruppierung wiederholter Ereignisse inkl. Häufigkeit, erstem und letztem Auftreten
- Filter nach Schweregrad, Protokoll und Zeitraum sowie Volltextsuche
- Live-Zähler für Kritisch / Hoch / Warnungen
- **Detailansicht** als seitlicher Drawer inkl. Roh-XML und Schaltfläche zum Kopieren
- Dezente Animationen (Framer Motion) für neu auftretende High- und Critical-Ereignisse
- Automatische Aktualisierung (Systeminfos alle 30 s, Ereignisse alle 15 s – konfigurierbar)
- Manuelle Aktualisierungsschaltfläche
- Verständliche Lade- (Skeletons) und Fehlerzustände, auch je Bereich
- Einstellungen für Aktualisierungsintervalle und „Animationen reduzieren“
- Single-Instance-Mechanismus und automatisches Öffnen des Browsers

## Bewusst noch nicht enthaltene Funktionen

Diese Funktionen sind in dieser Version **absichtlich nicht** enthalten:

- Kein KI-Chatbot, keine KI-Anbindung, kein externer KI-Server
- Kein Login, keine Benutzerverwaltung
- Keine Datenbank
- Keine Anbindung an Wazuh, Tactical RMM, Velociraptor oder Cloud-Dienste
- Keine Reparaturaktionen
- Keine Ausführung frei eingegebener PowerShell-Befehle
- Keine Änderung von Registry, Diensten, Treibern oder Windows-Einstellungen
- Keine Telemetrie

Die Anwendung liest ausschließlich Informationen. Es finden keine Systemänderungen statt.

## Voraussetzungen für die Entwicklung

- **Windows 10/11 (x64)**
- **.NET 8 SDK** – https://dotnet.microsoft.com/download/dotnet/8.0
- **Node.js 18+** (empfohlen 20/22) inkl. npm – https://nodejs.org

Auf dem **Zielrechner** (nur Ausführung der veröffentlichten EXE) werden **weder Node.js
noch das .NET SDK** benötigt, da die Anwendung self-contained veröffentlicht wird.

## Installation

```powershell
# Repository/Ordner öffnen und Frontend-Abhängigkeiten installieren
./scripts/install-frontend.ps1
```

Alternativ manuell:

```powershell
cd frontend
npm install
```

## Start im Entwicklungsmodus

Im Entwicklungsmodus laufen Backend und Frontend getrennt. Der Vite-Dev-Server leitet
`/api`-Anfragen an das Backend weiter. Das Sitzungstoken wird im Entwicklungsmodus
**nicht** erzwungen, damit die getrennte Frontend-Entwicklung funktioniert.

Zwei Terminals verwenden:

```powershell
# Terminal 1 – Backend (http://127.0.0.1:5187)
./scripts/start-backend-dev.ps1

# Terminal 2 – Frontend-Dev-Server (http://127.0.0.1:5173)
./scripts/start-frontend-dev.ps1
```

Anschließend die Oberfläche im Browser öffnen: **http://127.0.0.1:5173**

## Vollständiger Build und Windows-EXE

Das gebaute Frontend wird direkt nach `backend/wwwroot` geschrieben und vom Backend als
statische Weboberfläche ausgeliefert. Im veröffentlichten Zustand wird **kein**
separater Node.js-Server benötigt.

Einzelschritte:

```powershell
# 1. Nur das Frontend bauen (Ausgabe -> backend/wwwroot)
./scripts/build-frontend.ps1

# 2. Vollständige Windows-EXE erstellen (Frontend-Build + Self-contained Publish)
./scripts/publish.ps1
```

Der Veröffentlichungsbefehl entspricht im Kern:

```powershell
dotnet publish backend -c Release -r win-x64 --self-contained true -o publish
```

In der Release-Konfiguration sind aktiviert:

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

**Hinweis zur Einzeldatei:** Die ausführbare Datei `WindowsDiagnosticApp.exe` ist eine
Single-File-Anwendung mit eingebettetem .NET-Runtime. Statische Inhalte
(`wwwroot`), `appsettings.json` und `web.config` werden bewusst **als Dateien neben der
EXE** ausgeliefert. Das ist bei ASP.NET Core der stabile Standardweg; Funktionalität und
Stabilität haben Vorrang vor einer erzwungenen Einzeldatei. Zur Weitergabe wird daher der
**gesamte `publish`-Ordner** kopiert.

## Speicherort des veröffentlichten Programms

Nach `./scripts/publish.ps1` liegt das Ergebnis in:

```
publish/
├── WindowsDiagnosticApp.exe   <- diese Datei starten
├── wwwroot/                   <- gebautes Frontend
├── appsettings.json           <- Grenzwerte / Ereignis-Konfiguration
└── web.config
```

Zum Starten `WindowsDiagnosticApp.exe` doppelklicken oder in einer Konsole ausführen.

## Benötigte Windows-Berechtigungen

- Für die **Protokolle „System“ und „Application“** genügen in der Regel normale
  Benutzerrechte.
- Manche Protokolle oder Meldungstexte erfordern **Administratorrechte**. Fehlen die
  Rechte, wird dies in der Oberfläche verständlich angezeigt (z. B. Hinweis „Für das
  Protokoll … fehlen die erforderlichen Berechtigungen“). Die Anwendung versucht **nicht**,
  Rechte zu umgehen.
- Für einen vollständigen Zugriff kann die EXE optional per Rechtsklick „Als Administrator
  ausführen“ gestartet werden. Für die erste Version ist ein normaler Start ausreichend.

## Lokale Netzwerkbindung

Der Webserver wird über `builder.WebHost.UseUrls("http://127.0.0.1:5187")` **ausschließlich
an die Loopback-Adresse** gebunden. Es erfolgt keine Bindung an `0.0.0.0` oder eine
Netzwerkadresse. Die Anwendung ist damit nur vom lokalen Rechner erreichbar. Es gibt keine
CORS-Freigabe für fremde Ursprünge und keine Kommunikation mit externen Servern.

## Sitzungstoken

Damit eine **fremde lokale Webseite** die API nicht ohne Weiteres missbrauchen kann, wird
beim Start ein **zufälliges Sitzungstoken** erzeugt (32 zufällige Bytes, kryptografisch
sicher):

1. Beim Start erzeugt das Backend ein Token (`SessionToken.Create`).
2. Beim Ausliefern der `index.html` wird der Platzhalter `__WDA_SESSION_TOKEN__` im
   Meta-Tag `x-session-token` durch das aktuelle Token ersetzt.
3. Das Frontend liest das Token aus dem Meta-Tag und sendet es bei **jeder** API-Anfrage
   im Header `X-Session-Token` mit.
4. Die `SessionTokenMiddleware` prüft bei allen `/api/*`-Anfragen (außer `/api/health`)
   das Token per zeitkonstantem Vergleich und akzeptiert nur lokale Host-Header.

Eine fremde Webseite kann das Token nicht auslesen (Same-Origin-Policy, keine CORS-Freigabe)
und muss zudem einen benutzerdefinierten Header setzen, was einfache seitenübergreifende
Anfragen verhindert. **Im Entwicklungsmodus** (`ASPNETCORE_ENVIRONMENT=Development`) wird
das Token nicht erzwungen, damit der getrennte Vite-Dev-Server funktioniert. In der
veröffentlichten EXE läuft die Anwendung als **Production** – das Token wird erzwungen.

## API-Endpunkte

| Methode | Pfad | Beschreibung |
| ------- | ---- | ------------ |
| GET | `/api/health` | Statusprüfung (ohne Token) |
| GET | `/api/system/summary` | Allgemeine Rechnerinformationen |
| GET | `/api/system/cpu` | Prozessorinformationen inkl. Auslastung |
| GET | `/api/system/memory` | Arbeitsspeicher inkl. Auslastung und Status |
| GET | `/api/system/gpus` | Alle Grafikkarten |
| GET | `/api/system/disks` | Lokale Datenträger inkl. Belegung und Status |
| GET | `/api/system/windows` | Windows-Edition, Version, Build, Updates |
| GET | `/api/events` | Aufbereitete, gruppierte Windows-Ereignisse |
| GET | `/api/events/{eventKey}` | Einzelnes (gruppiertes) Ereignis |

Der Ereignis-Endpunkt unterstützt Query-Parameter, z. B.:

```
/api/events?level=critical,error&hours=24&log=System&search=Kernel
```

- `level`: `critical`, `error`/`high`, `warning` (kommagetrennt)
- `hours`: Zeitraum in Stunden (1–168, Standard 24)
- `log`: nur konfigurierte Protokolle sind zugelassen
- `search`: Ereignis-ID, Quelle oder Text (max. 200 Zeichen)

Alle Eingaben werden serverseitig validiert und die Anzahl der zurückgegebenen Ereignisse
ist begrenzt (`Events:MaxEvents`, Standard 500).

### Grenzwerte (zentral konfigurierbar)

In `backend/appsettings.json`:

```json
"Thresholds": {
  "RamWarningPercent": 85,
  "RamCriticalPercent": 95,
  "DiskFreeWarningPercent": 15,
  "DiskFreeCriticalPercent": 5
}
```

- RAM-Auslastung ab 85 % → Warnung, ab 95 % → Kritisch
- Laufwerk mit weniger als 15 % frei → Warnung, weniger als 5 % → Kritisch

### Bekannte Ereignisse

Bekannte, besonders wichtige Ereignisse werden verständlich erklärt und – wo sinnvoll – im
Schweregrad angehoben. Die Zuordnung liegt zentral in
`backend/Services/KnownEventCatalog.cs`. Erkannt werden u. a.: Kernel-Power 41,
EventLog 6008, BugCheck 1001, WHEA-Logger 17/18/19, Disk 7/51/55/153, storahci 129,
stornvme 129, Ntfs 55, Service Control Manager 7000/7001/7023/7031/7034,
DNS Client 1014, Display 4101, Application Error 1000, Application Hang 1002 sowie
WindowsUpdateClient 20/31.

## Bekannte Einschränkungen

- **Videospeicher der GPU** wird über WMI (`AdapterRAM`) ermittelt und ist bei mehr als
  4 GB technisch unzuverlässig. Nicht plausible Werte werden als „Nicht verfügbar“ angezeigt.
- **Ausstehende Windows-Updates** werden bewusst **nicht** ermittelt, da dies ohne
  Systemänderung nicht zuverlässig möglich ist. Der Wert ist daher „Nicht verfügbar“.
- **Meldungstexte** einzelner Ereignisse lassen sich nicht immer auflösen; dann wird der
  technische Text sinnvoll gekürzt dargestellt. Die Rohdaten (XML) bleiben in der
  Detailansicht erreichbar.
- Es werden standardmäßig nur Ereignisse der **Ebenen Kritisch, Fehler und Warnung**
  gelesen; Informationsereignisse bleiben ausgeblendet.
- Das Protokoll `Microsoft-Windows-WindowsUpdateClient/Operational` ist nicht auf jedem
  System vorhanden; fehlt es, erscheint ein Hinweis, ohne dass die Anwendung fehlschlägt.
- Die Single-File-EXE liefert `wwwroot`, `appsettings.json` und `web.config` als
  Begleitdateien aus (siehe oben).
- Ein Tray-Symbol ist noch nicht enthalten; die Architektur ist dafür vorbereitet. Beenden
  erfolgt über `Strg+C` bzw. den Task-Manager.

## Fehlerbehebung

- **„Das Frontend wurde noch nicht gebaut“** – zuerst `./scripts/build-frontend.ps1`
  ausführen (schreibt nach `backend/wwwroot`).
- **API antwortet mit 401** – das Sitzungstoken fehlt oder ist ungültig. Die Oberfläche
  immer über die vom Backend ausgelieferte Adresse **http://127.0.0.1:5187** öffnen, damit
  das Token korrekt injiziert wird. Im Entwicklungsmodus die Adresse
  **http://127.0.0.1:5173** verwenden.
- **Ereignisse fehlen / Hinweis auf fehlende Berechtigungen** – die Anwendung ggf. als
  Administrator starten.
- **Port 5187 belegt** – eine bereits laufende Instanz erkennt der Single-Instance-
  Mechanismus und öffnet nur den Browser erneut. Andernfalls den belegenden Prozess
  beenden.
- **`dotnet` wird nicht gefunden** – .NET 8 SDK installieren und ein neues Terminal öffnen.

## Projektstruktur

```
Windows_analysie_assistant/
├── backend/                     ASP.NET Core Minimal API (.NET 8, net8.0-windows)
│   ├── Program.cs               Hosting, DI, Single-Instance, Middleware, Browserstart
│   ├── Endpoints/               REST-Endpunkte (ApiEndpoints.cs)
│   ├── Models/                  Typisierte Datenmodelle (System, Ereignisse, Status)
│   ├── Options/                 Konfigurierbare Grenzwerte und Ereignis-Optionen
│   ├── Services/                Windows-Abfragen und Ereignis-Logik
│   │   ├── SystemInfoService    Rechnerinfos über WMI, Registry, DriveInfo
│   │   ├── EventLogService      Windows-Ereignisprotokolle (lesend)
│   │   ├── KnownEventCatalog    Bekannte Ereignisse + Schweregrad-Zuordnung
│   │   ├── EventGrouper         Gruppierung wiederholter Ereignisse
│   │   ├── HealthEvaluator      Bewertung von RAM- und Datenträgerwerten
│   │   └── EventQueryParser     Validierung der Filter-/Suchparameter
│   ├── Infrastructure/          Sitzungstoken, Middleware, Browserstart, Static-Hosting
│   └── wwwroot/                 Ziel des Frontend-Builds (statische Weboberfläche)
├── frontend/                    React + TypeScript + Vite + Tailwind
│   ├── src/
│   │   ├── components/          UI-Bausteine (Statusleiste, Sidebar, Ereignisse, …)
│   │   ├── pages/               DashboardPage (Hauptseite)
│   │   ├── hooks/               Datenabruf, Polling, Einstellungen
│   │   ├── services/            API-Client inkl. Token-Header
│   │   ├── types/              TypeScript-Typen (spiegeln die Backend-Modelle)
│   │   └── lib/                 Formatierung und Statusfarben
│   ├── package.json
│   └── vite.config.ts           Build nach ../backend/wwwroot, Dev-Proxy auf /api
├── tests/                       xUnit-Tests (Logik + Health-Endpunkt)
├── scripts/                     PowerShell-Skripte (install/dev/build/publish)
├── WindowsDiagnosticApp.sln
└── README.md
```

Backend, Frontend, Datenmodelle und Windows-Abfragen sind sauber getrennt.

## Tests

Die Tests liegen unter `tests/` und decken u. a. ab:

- Zuordnung der Windows-Ebenen zu Critical / High / Warning
- Erkennung der bekannten Ereignisse
- Gruppierung wiederholter Ereignisse
- Berechnung der RAM- und Datenträger-Warnungen
- Verhalten bei einem nicht vorhandenen Ereignisprotokoll
- Verhalten bei fehlenden Berechtigungen (geschütztes Protokoll)
- API-Health-Endpunkt und Token-Schutz (401 ohne Token)

Ausführen:

```powershell
dotnet test
```

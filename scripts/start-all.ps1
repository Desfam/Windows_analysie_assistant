<#
  Startet die komplette Windows-Diagnose-Anwendung mit einem Befehl:
    1. Prueft, ob Ollama laeuft, und startet es bei Bedarf im Hintergrund.
    2. Baut das Frontend (Ausgabe -> backend/wwwroot).
    3. Startet das Backend, das die Oberflaeche ausliefert und den Browser
       automatisch oeffnet, sobald der Server erreichbar ist.

  Verwendung:
    ./scripts/start-all.ps1
    ./scripts/start-all.ps1 -SkipFrontendBuild
#>
param(
    [switch]$SkipFrontendBuild,
    [string]$OllamaBaseUrl = 'http://127.0.0.1:11434'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# In manchen Umgebungen ist "dotnet" erst nach einem Neustart der Shell im PATH.
# Sicherstellen, dass dotnet.exe in dieser Sitzung gefunden wird.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
                [System.Environment]::GetEnvironmentVariable('Path', 'User')
}

function Test-Ollama {
    try {
        Invoke-RestMethod "$OllamaBaseUrl/api/version" -TimeoutSec 2 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

Write-Host '==> Pruefe Ollama...' -ForegroundColor Cyan
if (Test-Ollama) {
    Write-Host '    Ollama laeuft bereits.' -ForegroundColor Green
}
else {
    $ollamaCmd = Get-Command ollama -ErrorAction SilentlyContinue
    if (-not $ollamaCmd) {
        Write-Host '    Ollama wurde nicht gefunden (Befehl "ollama" nicht im PATH).' -ForegroundColor Yellow
        Write-Host '    Bitte Ollama installieren oder manuell starten: https://ollama.com' -ForegroundColor Yellow
    }
    else {
        Write-Host '    Ollama laeuft nicht. Starte "ollama serve" im Hintergrund...' -ForegroundColor Yellow
        Start-Process -FilePath $ollamaCmd.Source -ArgumentList 'serve' -WindowStyle Hidden

        $deadline = (Get-Date).AddSeconds(20)
        while ((Get-Date) -lt $deadline -and -not (Test-Ollama)) {
            Start-Sleep -Milliseconds 500
        }

        if (Test-Ollama) {
            Write-Host '    Ollama ist jetzt erreichbar.' -ForegroundColor Green
        }
        else {
            Write-Host '    Ollama konnte nicht rechtzeitig gestartet werden. Die App startet trotzdem;' -ForegroundColor Yellow
            Write-Host '    die KI-Diagnose zeigt in diesem Fall "nicht erreichbar" an.' -ForegroundColor Yellow
        }
    }
}

if (-not $SkipFrontendBuild) {
    Write-Host '==> Baue Frontend...' -ForegroundColor Cyan
    Push-Location (Join-Path $root 'frontend')
    try {
        if (-not (Test-Path 'node_modules')) {
            npm install
        }
        npm run build
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host '==> Frontend-Build uebersprungen (-SkipFrontendBuild).' -ForegroundColor Yellow
}

# Sicherstellen, dass das gebaute Frontend tatsaechlich vorhanden ist.
$indexHtml = Join-Path $root 'backend\wwwroot\index.html'
if (-not (Test-Path $indexHtml)) {
    Write-Host "    Kein gebautes Frontend gefunden ($indexHtml)." -ForegroundColor Red
    Write-Host '    Bitte das Frontend bauen (ohne -SkipFrontendBuild ausfuehren oder ./scripts/build-frontend.ps1).' -ForegroundColor Red
    throw 'Frontend-Build fehlt.'
}

Write-Host '==> Baue Backend...' -ForegroundColor Cyan
Push-Location (Join-Path $root 'backend')
try {
    dotnet build --configuration Debug | Out-Null
}
finally {
    Pop-Location
}

Write-Host '==> Starte Backend (oeffnet die App automatisch im Browser)...' -ForegroundColor Cyan
# Production erzwingen, damit das Sitzungstoken wie im veroeffentlichten Betrieb geprueft wird.
# Die kompilierte DLL wird direkt gestartet, damit launchSettings.json (Development) nicht greift.
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$backendDir = Join-Path $root 'backend'
$dll = Join-Path $backendDir 'bin\Debug\net8.0-windows\win-x64\WindowsDiagnosticApp.dll'
if (-not (Test-Path $dll)) {
    throw "Backend-DLL nicht gefunden: $dll. Wurde der Backend-Build erfolgreich abgeschlossen?"
}
# ContentRoot explizit auf das Backend-Projekt setzen. Wird die DLL direkt gestartet,
# verwendet ASP.NET sonst das aktuelle Arbeitsverzeichnis und findet weder
# wwwroot (das gebaute Frontend) noch appsettings.json.
dotnet $dll --contentRoot $backendDir


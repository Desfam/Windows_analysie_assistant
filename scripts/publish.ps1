# Erstellt die vollständige, eigenständige Windows-EXE (Self-contained, win-x64).
# Ablauf:
#   1. Frontend bauen (Ausgabe nach backend/wwwroot)
#   2. Backend als Self-contained Single-File veröffentlichen
#
# Die veröffentlichte Anwendung benötigt auf dem Zielrechner weder das .NET SDK
# noch Node.js.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'publish'

Write-Host '==> Frontend wird gebaut...' -ForegroundColor Cyan
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

Write-Host '==> Backend wird veröffentlicht (Self-contained, win-x64)...' -ForegroundColor Cyan
Push-Location (Join-Path $root 'backend')
try {
    dotnet publish -c Release -r win-x64 --self-contained true -o $publishDir
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host "==> Fertig. Ausgabe: $publishDir" -ForegroundColor Green
Write-Host '    Starten Sie WindowsDiagnosticApp.exe in diesem Ordner.' -ForegroundColor Green

# Startet das Frontend im Entwicklungsmodus (Vite-Dev-Server auf Port 5173).
# Voraussetzung: Das Backend läuft parallel unter http://127.0.0.1:5187
# (siehe start-backend-dev.ps1). Die /api-Anfragen werden an das Backend weitergeleitet.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location (Join-Path $root 'frontend')
try {
    npm run dev
}
finally {
    Pop-Location
}

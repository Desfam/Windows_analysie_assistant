# Installiert die Frontend-Abhängigkeiten.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location (Join-Path $root 'frontend')
try {
    npm install
}
finally {
    Pop-Location
}

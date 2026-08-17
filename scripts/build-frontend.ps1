# Baut das Frontend. Vite schreibt die Ausgabe direkt nach backend/wwwroot,
# sodass das Backend das gebaute Frontend als statische Weboberfläche ausliefert.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location (Join-Path $root 'frontend')
try {
    npm run build
}
finally {
    Pop-Location
}

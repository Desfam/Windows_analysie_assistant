# Startet das Backend im Entwicklungsmodus.
# Im Entwicklungsmodus wird das Sitzungstoken nicht erzwungen, damit der
# getrennte Vite-Dev-Server ohne Token-Injektion auf die API zugreifen kann.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location (Join-Path $root 'backend')
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet run
}
finally {
    Pop-Location
}

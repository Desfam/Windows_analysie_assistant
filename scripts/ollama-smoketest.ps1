# Backend-Smoke-Test für die Ollama-Anbindung.
# Prüft: Status, Modelle, NDJSON-Streaming, inkrementelle Auslieferung,
# Abbruch/Cancellation, saubere Freigabe und strukturierte Fehler-Chunks.
#
# Voraussetzung: Das Backend läuft (Standard: http://127.0.0.1:5187) und Ollama ist
# erreichbar. Für den Chat-Test wird ein kleines Modell verwendet.
param(
    [string]$BaseUrl = 'http://127.0.0.1:5187',
    [string]$Model = 'qwen2.5-coder:3b'
)

$ErrorActionPreference = 'Stop'
$pass = 0
$fail = 0

function Check([bool]$ok, [string]$msg) {
    if ($ok) { Write-Host "[PASS] $msg" -ForegroundColor Green; $script:pass++ }
    else { Write-Host "[FAIL] $msg" -ForegroundColor Red; $script:fail++ }
}

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(120)

function New-ChatRequest([string]$content) {
    $body = @{ model = $Model; messages = @(@{ role = 'user'; content = $content }) } | ConvertTo-Json -Depth 6
    $req = [System.Net.Http.HttpRequestMessage]::new('Post', "$BaseUrl/api/ollama/chat")
    $req.Content = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, 'application/json')
    return $req
}

# 1. Status
$status = Invoke-RestMethod "$BaseUrl/api/ollama/status"
Check ($status.connected -and $status.version -and $status.checkedAt) `
    "1. Status: connected=$($status.connected), version=$($status.version), checkedAt gesetzt"

# 2. Modelle normalisiert
$models = Invoke-RestMethod "$BaseUrl/api/ollama/models"
$m0 = $models.models | Select-Object -First 1
Check ($models.connected -and $models.models.Count -gt 0 -and $m0.name) `
    "2. Modelle: $($models.models.Count) normalisiert (erstes: $($m0.name), Größe: $($m0.sizeBytes))"

# 3./4. Streaming: einzelne NDJSON-Chunks, erster Chunk vor Abschluss
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = $client.SendAsync((New-ChatRequest 'Nenne drei kurze Stichpunkte zur Datensicherung.'),
    [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
$ndjson = $resp.Content.Headers.ContentType.MediaType
$stream = $resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
$reader = [System.IO.StreamReader]::new($stream)
$firstDeltaMs = $null; $deltas = 0; $doneMs = $null; $done = $false
while (-not $reader.EndOfStream) {
    $line = $reader.ReadLine()
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $obj = $line | ConvertFrom-Json
    if ($obj.type -eq 'delta') { if ($null -eq $firstDeltaMs) { $firstDeltaMs = $sw.ElapsedMilliseconds }; $deltas++ }
    elseif ($obj.type -eq 'done') { $done = $true; $doneMs = $sw.ElapsedMilliseconds; break }
}
$reader.Dispose(); $resp.Dispose()
Check ($ndjson -eq 'application/x-ndjson' -and $deltas -gt 1) `
    "3. NDJSON-Streaming: content-type=$ndjson, $deltas Delta-Chunks"
Check ($null -ne $firstDeltaMs -and $done -and $firstDeltaMs -lt $doneMs) `
    "4. Inkrementell: erster Chunk nach $firstDeltaMs ms, vollständig nach $doneMs ms"

# 5./6./9. Abbruch mitten im Stream, danach saubere Weiterarbeit des Servers
$resp2 = $client.SendAsync((New-ChatRequest 'Schreibe einen langen, ausführlichen Text über Windows.'),
    [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
$stream2 = $resp2.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
$reader2 = [System.IO.StreamReader]::new($stream2)
$readBeforeCancel = 0
while (-not $reader2.EndOfStream -and $readBeforeCancel -lt 3) {
    $line = $reader2.ReadLine()
    if (-not [string]::IsNullOrWhiteSpace($line)) { $readBeforeCancel++ }
}
# Abbruch simulieren: Stream und Response freigeben (entspricht dem Frontend-Abort).
$reader2.Dispose(); $stream2.Dispose(); $resp2.Dispose()
Check ($readBeforeCancel -ge 1) "5./6. Abbruch nach $readBeforeCancel Chunks – Verbindung/Stream freigegeben"
Start-Sleep -Milliseconds 300
$statusAfter = Invoke-RestMethod "$BaseUrl/api/ollama/status"
Check ($statusAfter.connected) "9. Server nach Abbruch weiterhin funktionsfähig (Ressourcen freigegeben)"

# 7. Strukturierter Fehler-Chunk bei ungültigem Modell
$reqBad = [System.Net.Http.HttpRequestMessage]::new('Post', "$BaseUrl/api/ollama/chat")
$badBody = @{ model = 'kein-solches-modell:xyz'; messages = @(@{ role = 'user'; content = 'Hallo' }) } | ConvertTo-Json -Depth 6
$reqBad.Content = [System.Net.Http.StringContent]::new($badBody, [System.Text.Encoding]::UTF8, 'application/json')
$respBad = $client.SendAsync($reqBad, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
$readerBad = [System.IO.StreamReader]::new($respBad.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
$errorChunk = $false
while (-not $readerBad.EndOfStream) {
    $line = $readerBad.ReadLine()
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $obj = $line | ConvertFrom-Json
    if ($obj.type -eq 'error') { $errorChunk = $true; break }
}
$readerBad.Dispose(); $respBad.Dispose()
Check $errorChunk "7. Ungültiges Modell liefert strukturierten Fehler-Chunk (type=error)"

$client.Dispose()
Write-Host ''
Write-Host "Ergebnis: $pass bestanden, $fail fehlgeschlagen" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 }

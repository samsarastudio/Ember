$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $taskRoot '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $runtime)) { $runtime = 'dotnet' }
$env:ASPNETCORE_URLS = 'http://localhost:5080'
$env:EMBERWAKE_BATTLE_TRANSPORT = 'fusion'
$env:EMBERWAKE_REMOTE_WORKER = '0'
$env:EMBERWAKE_DB = Join-Path $taskRoot 'Backend/data/emberwake.db'
$env:EMBERWAKE_MATCH_EXECUTABLE=Join-Path $taskRoot 'Builds/Windows/Emberwake.exe'
$env:EMBERWAKE_MATCH_DATA=Join-Path $taskRoot 'Backend/data/matches'
if(!$env:EMBERWAKE_MATCH_REGION){$env:EMBERWAKE_MATCH_REGION='us'}
if ([string]::IsNullOrWhiteSpace($env:EMBERWAKE_ADMIN_TOKEN)) {
    $secretDirectory = Join-Path $taskRoot '.tools'
    New-Item -ItemType Directory -Force -Path $secretDirectory | Out-Null
    $secretFile = Join-Path $secretDirectory 'local-admin-token.txt'
    if (!(Test-Path -LiteralPath $secretFile)) {
        $bytes = New-Object byte[] 32
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        [IO.File]::WriteAllText($secretFile, [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant())
    }
    $env:EMBERWAKE_ADMIN_TOKEN = [IO.File]::ReadAllText($secretFile).Trim()
    Write-Host "Local admin token file: $secretFile (do not share or commit it)."
}
Push-Location (Join-Path $taskRoot 'Backend')
try { & $runtime run --project Emberwake.Backend.csproj } finally { Pop-Location }

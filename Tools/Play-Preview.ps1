$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$game=Join-Path $taskRoot 'Builds/Windows/Emberwake.exe'
if (!(Test-Path -LiteralPath $game)) { throw 'Build the Windows preview first with Tools/Build-Preview.ps1.' }
$ready=$false
try { $health=Invoke-RestMethod 'http://localhost:5080/health' -TimeoutSec 2; $ready=$health.status -eq 'ok' } catch {}
if (!$ready) {
    $shellExecutable=(Get-Process -Id $PID).Path
    $backendScript=Join-Path $PSScriptRoot 'Start-Backend.ps1'
    Start-Process -FilePath $shellExecutable -ArgumentList "-NoProfile -File `"$backendScript`"" -WindowStyle Hidden
    for($attempt=0;$attempt -lt 20;$attempt++) {
        Start-Sleep -Milliseconds 500
        try { $health=Invoke-RestMethod 'http://localhost:5080/health' -TimeoutSec 1; if($health.status -eq 'ok'){$ready=$true;break} } catch {}
    }
}
if (!$ready) { Write-Warning 'The backend did not start. The game can still run offline training.' }
Start-Process -FilePath $game -ArgumentList @('-api','http://localhost:5080') -WindowStyle Normal

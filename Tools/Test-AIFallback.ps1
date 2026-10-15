# Exercises the live release app against the server AI; no second client is launched.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$logs=Join-Path $root ('Builds/AIVerification/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$log=Join-Path $logs 'client.log'
$client=$null
try {
    $client=Start-Process (Join-Path $root 'Builds/Release/Windows/Emberwake.exe') -ArgumentList @('-batchmode','-ranked-autotest','-fusion-autotest','-ai-autotest','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    Write-Output "AI verification logs: $logs"
    $deadline=(Get-Date).AddSeconds(180)
    while((Get-Date) -lt $deadline -and !$client.HasExited){Start-Sleep -Milliseconds 500}
    if(!$client.HasExited -or $client.ExitCode -ne 0){throw 'AI client failed or timed out'}
    if(!(Select-String -LiteralPath $log -Pattern 'FUSION_CLIENT_RESULT team=0 .*bothFleets=True pass=True')){throw 'AI combat audit did not pass'}
    $matches=Join-Path $root 'Builds/Fusion-Windows-Worker-20261014/state/matches'
    $assignment=Get-ChildItem -LiteralPath $matches -Directory | Sort-Object LastWriteTime -Descending | ForEach-Object {
        $path=Join-Path $_.FullName 'assignment.json'
        if(Test-Path -LiteralPath $path){$a=Get-Content -Raw -LiteralPath $path | ConvertFrom-Json;if($a.aiOpponent -and (Select-String -LiteralPath $log -SimpleMatch $a.room)){$a;break}}
    } | Select-Object -First 1
    if(!$assignment){throw 'Could not correlate client to an authoritative AI assignment'}
    $settled=Join-Path $matches ($assignment.matchId+'/settled')
    for($i=0;$i -lt 10 -and !(Test-Path -LiteralPath $settled);$i++){Start-Sleep -Seconds 1}
    if(!(Test-Path -LiteralPath $settled)){throw 'Authoritative AI result was not settled'}
    Write-Output 'PASS: lone release client matched server AI, observed both fleets, attacks, impacts and damage, and the result settled.'
} finally {
    if($client -and !$client.HasExited){$client.Kill($true);$client.WaitForExit()}
}

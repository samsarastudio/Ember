param([switch]$Reconnect,[switch]$ApiSuite)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$game=Join-Path $taskRoot 'Builds/Windows/Emberwake.exe'
$logs=Join-Path $taskRoot ('Builds/PiServerTests/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$origin='http://127.0.0.1:5099'
$processes=@()
try {
    $env:ASPNETCORE_URLS=$origin
    $env:EMBERWAKE_DB=Join-Path $logs 'test.db'
    $env:EMBERWAKE_MATCH_DATA=Join-Path $logs 'matches'
    $env:EMBERWAKE_BATTLE_TRANSPORT='websocket'
    $env:EMBERWAKE_MATCH_CAPACITY='4'
    $env:EMBERWAKE_MATCH_EXECUTABLE=''
    $api=Start-Process -FilePath (Join-Path $taskRoot '.tools/dotnet/dotnet.exe') -ArgumentList ('"'+(Join-Path $taskRoot 'Backend/bin/Debug/net10.0/Emberwake.Backend.dll')+'"') -WorkingDirectory (Join-Path $taskRoot 'Backend') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'api.log') -RedirectStandardError (Join-Path $logs 'api-errors.log')
    $processes+=$api
    $ready=$false
    for($i=0;$i -lt 40;$i++){try{$health=Invoke-RestMethod "$origin/health";if($health.transport -eq 'websocket'){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 500}
    if(!$ready){throw 'Pi-mode API did not become ready'}
    if($ApiSuite){
        & (Join-Path $taskRoot '.tools/dotnet/dotnet.exe') run --project (Join-Path $taskRoot 'Tests/Emberwake.Tests.csproj') -- $origin
        if($LASTEXITCODE -ne 0){throw 'Account/HTTP regression suite failed'}
    }
    $accounts=@()
    function Start-Captain($number,$credential){
        $clientArgs=@('-batchmode','-screen-width','540','-screen-height','960','-ranked-autotest','-api',$origin,'-test-credential',$credential,'-logFile',('"'+(Join-Path $logs "client$number.log")+'"'))
        Start-Process -FilePath $game -ArgumentList $clientArgs -WindowStyle Hidden -PassThru
    }
    foreach($n in 1..2){
        $account=Invoke-RestMethod "$origin/v1/auth/guest" -Method Post -ContentType 'application/json' -Body ('{"name":"Pi Test '+$n+'"}')
        $accounts+=$account;$processes+=Start-Captain $n $account.credential
    }
    $match=$null
    for($i=0;$i -lt 60;$i++){
        $match=Invoke-RestMethod "$origin/v1/queue" -Headers @{Authorization='Bearer '+$accounts[0].token}
        if($match.status -eq 'matched'){break};Start-Sleep -Milliseconds 500
    }
    if($match.status -ne 'matched'){throw 'Automatic matchmaking did not complete'}
    if($Reconnect){
        Start-Sleep -Seconds 12
        $processes[1].Kill($true);$processes[1].WaitForExit()
        Move-Item -LiteralPath (Join-Path $logs 'client1.log') -Destination (Join-Path $logs 'client1-before-reconnect.log')
        $processes[1]=Start-Captain 1 $accounts[0].credential
    }
    $deadline=(Get-Date).AddSeconds(150)
    while((Get-Date) -lt $deadline -and @($processes | Select-Object -Skip 1 | Where-Object {!$_.HasExited}).Count -gt 0){Start-Sleep -Milliseconds 500}
    foreach($client in ($processes | Select-Object -Skip 1)){if(!$client.HasExited -or $client.ExitCode -ne 0){throw 'Client timed out or failed'}}
    $outcome=Invoke-RestMethod "$origin/v1/matches/$($match.matchId)" -Headers @{Authorization='Bearer '+$accounts[0].token}
    foreach($n in 1..2){
        $result=Select-String -LiteralPath (Join-Path $logs "client$n.log") -Pattern 'PI_CLIENT_RESULT team=(\d+) tick=(\d+) winner=(-?\d+) hash=(-?\d+).*pass=True'
        if(!$result){throw "Client $n combat coverage failed"}
        if([int]$result.Matches[0].Groups[2].Value -ne $outcome.tick -or [int]$result.Matches[0].Groups[3].Value -ne $outcome.winner -or [int]$result.Matches[0].Groups[4].Value -ne $outcome.publicHash){throw 'Client state differs from authority'}
        $profile=Invoke-RestMethod "$origin/v1/profile" -Headers @{Authorization='Bearer '+$accounts[$n-1].token}
        if($profile.battles -ne 1 -or $profile.history[0].id -ne $match.matchId){throw 'Ranked rewards were not persisted exactly once'}
    }
    Write-Output "PASS: Pi-compatible server mode, automatic trophy matchmaking, shared projectiles/impacts/damage, final public hash, and persisted rewards. Reconnect=$Reconnect. Logs: $logs"
} finally {
    foreach($process in $processes){if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()}}
}

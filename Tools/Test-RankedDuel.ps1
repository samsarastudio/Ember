param([ValidateSet('us','usw','cae')][string]$Region='us',[switch]$Impaired,[switch]$Reconnect,[switch]$Remote)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$game=Join-Path $taskRoot 'Builds/Windows/Emberwake.exe'
$logs=Join-Path $taskRoot ('Builds/RankedTests/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$origin='http://127.0.0.1:5097'
$processes=@();$worker=$null
try {
    $env:ASPNETCORE_URLS=$origin
    $env:EMBERWAKE_BATTLE_TRANSPORT='fusion'
    if($Remote){$env:EMBERWAKE_REMOTE_WORKER='1';$env:EMBERWAKE_WORKER_KEY=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')}else{$env:EMBERWAKE_REMOTE_WORKER='0'}
    $env:EMBERWAKE_DB=Join-Path $logs 'test.db'
    $env:EMBERWAKE_MATCH_EXECUTABLE=$game
    $env:EMBERWAKE_MATCH_DATA=Join-Path $logs 'matches'
    $env:EMBERWAKE_MATCH_REGION=$Region
    $env:EMBERWAKE_MATCH_CAPACITY='1'
    $api=Start-Process -FilePath (Join-Path $taskRoot '.tools/dotnet/dotnet.exe') -ArgumentList ('"'+(Join-Path $taskRoot 'Backend/bin/Debug/net10.0/Emberwake.Backend.dll')+'"') -WorkingDirectory (Join-Path $taskRoot 'Backend') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'api.log') -RedirectStandardError (Join-Path $logs 'api-errors.log')
    $processes+=$api
    if($Remote){
        $env:EMBERWAKE_COORDINATOR=$origin
        $env:EMBERWAKE_MATCH_DATA=Join-Path $logs 'worker-matches'
        $worker=Start-Process -FilePath (Join-Path $taskRoot '.tools/dotnet/dotnet.exe') -ArgumentList ('"'+(Join-Path $taskRoot 'Backend/bin/Debug/net10.0/Emberwake.Backend.dll')+'" --worker') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'worker.log') -RedirectStandardError (Join-Path $logs 'worker-errors.log')
    }
    $ready=$false
    for($i=0;$i -lt 40;$i++){try{$health=Invoke-RestMethod "$origin/health";if($health.rankedAvailable){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 500}
    if(!$ready){throw 'Ranked API did not become ready'}
    if($Remote){
        try{Invoke-RestMethod "$origin/internal/fusion/poll" -Method Post -ContentType 'application/json' -Body '{"worker":"intruder"}' | Out-Null;throw 'Unauthenticated worker accepted'}catch{if([int]$_.Exception.Response.StatusCode -ne 401){throw}}
        try{Invoke-RestMethod "$origin/internal/fusion/report" -Method Post -ContentType 'application/json' -Headers @{'X-Worker-Key'=$env:EMBERWAKE_WORKER_KEY} -Body '{"worker":"intruder","matchId":"unknown","status":"complete"}' | Out-Null;throw 'Unknown result accepted'}catch{if([int]$_.Exception.Response.StatusCode -ne 409){throw}}
    }
    $accounts=@()
    foreach($n in 1..2){
        $account=Invoke-RestMethod "$origin/v1/auth/guest" -Method Post -ContentType 'application/json' -Body ('{"name":"Ranked Test '+$n+'"}')
        $accounts+=$account
        $clientArgs=@('-batchmode','-screen-width','540','-screen-height','960','-ranked-autotest','-fusion-autotest','-api',$origin,'-test-credential',$account.credential,'-logFile',('"'+(Join-Path $logs "client$n.log")+'"'))
        if($Impaired){$clientArgs+='-fusion-impaired'}
        $processes+=Start-Process -FilePath $game -ArgumentList $clientArgs -WindowStyle Hidden -PassThru
    }
    if($Reconnect){
        $connected=$false
        for($i=0;$i -lt 120;$i++){
            if((Test-Path -LiteralPath (Join-Path $logs 'client1.log')) -and (Select-String -LiteralPath (Join-Path $logs 'client1.log') -Pattern 'FUSION_BATTLE_CONNECTED' -Quiet)){$connected=$true;break}
            Start-Sleep -Milliseconds 500
        }
        if(!$connected){throw 'First client did not connect'}
        Start-Sleep -Seconds 12
        $processes[1].Kill($true);$processes[1].WaitForExit()
        Move-Item -LiteralPath (Join-Path $logs 'client1.log') -Destination (Join-Path $logs 'client1-before-reconnect.log')
        # Reuse the same account, not a client-supplied team. The API returns its existing match ticket.
        $rejoinArgs=@('-batchmode','-screen-width','540','-screen-height','960','-ranked-autotest','-fusion-autotest','-api',$origin,'-test-credential',$accounts[0].credential,'-logFile',('"'+(Join-Path $logs 'client1.log')+'"'))
        if($Impaired){$rejoinArgs+='-fusion-impaired'}
        $processes[1]=Start-Process -FilePath $game -ArgumentList $rejoinArgs -WindowStyle Hidden -PassThru
    }
    $deadline=(Get-Date).AddSeconds(180)
    while((Get-Date) -lt $deadline -and @($processes | Select-Object -Skip 1 | Where-Object {!$_.HasExited}).Count -gt 0){Start-Sleep -Milliseconds 500}
    foreach($client in ($processes | Select-Object -Skip 1)){if(!$client.HasExited -or $client.ExitCode -ne 0){throw 'Client timed out or failed'}}
    $resultFiles=@(Get-ChildItem -LiteralPath (Join-Path $logs 'matches') -Filter result.json -Recurse)
    if($resultFiles.Count -ne 1){throw 'Expected exactly one authoritative result'}
    $outcome=Get-Content -Raw -LiteralPath $resultFiles[0].FullName | ConvertFrom-Json
    if($Remote){
        $workerId=(Get-Content -Raw -LiteralPath (Join-Path $logs 'worker-matches/worker-id')).Trim()
        $report=@{worker=$workerId;matchId=$outcome.matchId;status='complete';outcome=$outcome}|ConvertTo-Json -Depth 8
        Invoke-RestMethod "$origin/internal/fusion/report" -Method Post -ContentType 'application/json' -Headers @{'X-Worker-Key'=$env:EMBERWAKE_WORKER_KEY} -Body $report | Out-Null
    }
    foreach($n in 1..2){
        $result=Select-String -LiteralPath (Join-Path $logs "client$n.log") -Pattern 'FUSION_CLIENT_RESULT team=(\d+) tick=(\d+) winner=(-?\d+) hash=(-?\d+).*pass=True'
        if(!$result){throw "Client $n combat coverage failed"}
        if([int]$result.Matches[0].Groups[2].Value -ne $outcome.tick -or [int]$result.Matches[0].Groups[3].Value -ne $outcome.winner -or [int]$result.Matches[0].Groups[4].Value -ne $outcome.hash){throw 'Client state differs from authority'}
        $headers=@{Authorization='Bearer '+$accounts[$n-1].token}
        $profile=Invoke-RestMethod "$origin/v1/profile" -Headers $headers
        if($profile.battles -ne 1 -or $profile.history[0].id -ne $outcome.matchId){throw 'Authoritative rewards not persisted exactly once'}
    }
    Write-Output "PASS: automatic matchmaking, ticket admission, both fleets/projectiles/damage, identical final state, and one persisted result per account. Logs: $logs"
} finally {
    # Stop only processes started by this test, including their match children.
    if($worker -and !$worker.HasExited){$worker.Kill($true);$worker.WaitForExit()}
    foreach($process in $processes){if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()}}
}

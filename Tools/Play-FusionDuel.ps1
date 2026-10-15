param(
    [ValidateSet('us','usw','cae')][string]$Region='us',
    [string]$Room=('FLEET-'+[Guid]::NewGuid().ToString('N').Substring(0,8)),
    [switch]$Automated,
    [switch]$ServerOnly,
    [switch]$Impaired,
    [switch]$ClientHost
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$game=Join-Path $taskRoot 'Builds/Windows/Emberwake.exe'
if (!(Test-Path -LiteralPath $game)) { throw 'Build the Windows player first with Tools/Build-Preview.ps1.' }
$logs=Join-Path $taskRoot ('Builds/FusionBattleTests/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$processes=@()
try {
    $serverLog=Join-Path $logs 'server.log'
    $serverArgs=@('-batchmode','-nographics','-fusion-server','-fusion-room',$Room,'-fusion-region',$Region,'-logFile',('"'+$serverLog+'"'))
    if($ClientHost){
        $serverLog=Join-Path $logs 'client1.log'
        $serverArgs=@('-screen-width','540','-screen-height','960','-screen-fullscreen','0','-fusion-host','-fusion-room',$Room,'-fusion-region',$Region,'-logFile',('"'+$serverLog+'"'))
        if($Automated){$serverArgs+=@('-batchmode','-fusion-capture',('"'+(Join-Path $logs 'client1.png')+'"'))}
    }
    if($Automated){$serverArgs+='-fusion-autotest'}
    if($Impaired){$serverArgs+='-fusion-impaired'}
    if($ClientHost -and !$Automated){$processes+=Start-Process -FilePath $game -ArgumentList $serverArgs -PassThru}
    else{$processes+=Start-Process -FilePath $game -ArgumentList $serverArgs -WindowStyle Hidden -PassThru}
    $deadline=(Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 500
        if($processes[0].HasExited){throw "Server exited. See $serverLog"}
        $ready=(Test-Path -LiteralPath $serverLog) -and (Select-String -LiteralPath $serverLog -Pattern 'FUSION_BATTLE_CONNECTED' -Quiet)
    } until($ready -or (Get-Date) -gt $deadline)
    if(!$ready){throw "Server connection timed out. See $serverLog"}
    Write-Output "Room: $Room | Region: $Region | Logs: $logs"
    if(!$ServerOnly){
        $clientNumbers=if($ClientHost){@(2)}else{@(1,2)}
        foreach($n in $clientNumbers){
            $clientLog=Join-Path $logs "client$n.log"
            $capture=Join-Path $logs "client$n.png"
            $clientArgs=@('-screen-width','540','-screen-height','960','-screen-fullscreen','0','-fusion-room',$Room,'-fusion-region',$Region,'-logFile',('"'+$clientLog+'"'))
            if($Automated){$clientArgs+=@('-batchmode','-fusion-autotest','-fusion-capture',('"'+$capture+'"'))}
            if($Impaired){$clientArgs+='-fusion-impaired'}
            if($Automated){$processes+=Start-Process -FilePath $game -ArgumentList $clientArgs -WindowStyle Hidden -PassThru}
            else{$processes+=Start-Process -FilePath $game -ArgumentList $clientArgs -PassThru}
        }
    }
    if($Automated){
        foreach($process in $processes){
            if(!$process.WaitForExit(180000)){throw "Battle test timed out. See $logs"}
            if($process.ExitCode -ne 0){throw "Battle test failed (exit $($process.ExitCode)). See $logs"}
        }
        $serverResult=Select-String -LiteralPath $serverLog -Pattern 'FUSION_SERVER_RESULT tick=(\d+) winner=(-?\d+) hash=(-?\d+)'
        if(!$serverResult){throw "Missing server result: $logs"}
        foreach($n in 1..2){
            $result=Select-String -LiteralPath (Join-Path $logs "client$n.log") -Pattern 'FUSION_CLIENT_RESULT team=(\d+) tick=(\d+) winner=(-?\d+) hash=(-?\d+).*pass=True'
            if(!$result){throw "Client $n did not pass combat coverage: $logs"}
            for($i=1;$i -le 3;$i++){
                if($serverResult.Matches[0].Groups[$i].Value -ne $result.Matches[0].Groups[$i+1].Value){throw "Client $n final state differs from server: $logs"}
            }
        }
        Write-Output 'PASS: both clients replicated both fleets, attacks, impacts and damage; final tick, winner and world hash match the server.'
    }else{
        Write-Output 'The match server is running. Close this launcher after the duel to stop it.'
        $processes[0].WaitForExit()
    }
}finally{
    foreach($process in $processes){if(!$process.HasExited){Stop-Process -Id $process.Id}}
}

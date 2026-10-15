param([string]$Api='http://127.0.0.1:5080',[switch]$AutoTest)
$ErrorActionPreference='Stop'
$Api=$Api.TrimEnd('/')
$uri=[Uri]$Api
if($uri.Scheme -ne 'https' -and !($uri.Scheme -eq 'http' -and $uri.IsLoopback)){throw 'Use HTTPS or the local test server.'}
$health=Invoke-RestMethod "$Api/health" -TimeoutSec 5
if($health.transport -ne 'fusion' -or !$health.rankedAvailable){throw 'Start the Fusion server and worker first. The server is not ready for ranked battles.'}
$game=Join-Path $PSScriptRoot 'Game/Emberwake.exe'
if(!(Test-Path -LiteralPath $game)){throw 'Extract the complete Windows test package first.'}
$state=Join-Path $PSScriptRoot 'state/two-windows'
New-Item -ItemType Directory -Path $state -Force | Out-Null
$logs=Join-Path $PSScriptRoot ('test-logs/two-windows-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$processes=@();$accounts=@()
foreach($slot in 1..2){
    $file=Join-Path $state "captain-$slot.json"
    $saved=if(Test-Path -LiteralPath $file){Get-Content -Raw -LiteralPath $file | ConvertFrom-Json}else{$null}
    $credential=if($saved -and $saved.api -eq $Api){$saved.credential}else{''}
    $body=@{name="Windows Captain $slot";credential=$credential}|ConvertTo-Json
    $account=Invoke-RestMethod "$Api/v1/auth/guest" -Method Post -ContentType 'application/json' -Body $body
    @{api=$Api;credential=$account.credential}|ConvertTo-Json|Set-Content -LiteralPath $file
    $accounts+=$account
    # These are dedicated test captains; open the harbor without repeating onboarding.
    Invoke-RestMethod "$Api/v1/profile/tutorial" -Method Post -Headers @{Authorization='Bearer '+$account.token} | Out-Null
}
if($accounts[0].profile.id -eq $accounts[1].profile.id){throw 'Test accounts must be distinct.'}
foreach($slot in 1..2){
    $clientArgs=@('-api',$Api,'-test-credential',$accounts[$slot-1].credential,'-screen-width','540','-screen-height','960','-logFile',('"'+(Join-Path $logs "captain-$slot.log")+'"'))
    if($AutoTest){$clientArgs+=@('-batchmode','-ranked-autotest','-fusion-autotest');$processes+=Start-Process $game -ArgumentList $clientArgs -WindowStyle Hidden -PassThru}
    else{$processes+=Start-Process $game -ArgumentList $clientArgs -PassThru}
}
Write-Host "Opened two separate captains connected to $Api. Press Battle in both windows. Logs: $logs"
if($AutoTest){
    try{
        $deadline=(Get-Date).AddSeconds(180)
        while((Get-Date) -lt $deadline -and @($processes | Where-Object {!$_.HasExited}).Count -gt 0){Start-Sleep -Milliseconds 500}
        $results=@()
        foreach($slot in 1..2){
            if(!$processes[$slot-1].HasExited -or $processes[$slot-1].ExitCode -ne 0){throw "Captain $slot failed or timed out"}
            $line=Select-String -LiteralPath (Join-Path $logs "captain-$slot.log") -Pattern 'FUSION_CLIENT_RESULT team=(\d+) tick=(\d+) winner=(-?\d+) hash=(-?\d+).*pass=True'
            if(!$line){throw "Captain $slot combat validation failed"}
            $results+=$line.Matches[0]
        }
        if($results[0].Groups[1].Value -eq $results[1].Groups[1].Value){throw 'Both clients received the same team'}
        foreach($field in 2..4){if($results[0].Groups[$field].Value -ne $results[1].Groups[$field].Value){throw 'Client battle states differ'}}
        Write-Host 'PASS: two independent accounts joined opposite teams and agreed on the final combat state.'
    }finally{foreach($process in $processes){if(!$process.HasExited){& taskkill.exe /PID $process.Id /T /F | Out-Null;$process.WaitForExit()}}}
}

param([switch]$InstallAndroid,[switch]$Lan,[ValidateSet('us','usw','cae')][string]$Region='us')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$env:ASPNETCORE_URLS=if($Lan){'http://0.0.0.0:5080'}else{'http://127.0.0.1:5080'}
$env:EMBERWAKE_DB=Join-Path $root 'Backend/data/mobile-test.db'
$env:EMBERWAKE_BATTLE_TRANSPORT='fusion'
$env:EMBERWAKE_REMOTE_WORKER='1'
$env:EMBERWAKE_MATCH_REGION=$Region
$env:EMBERWAKE_MATCH_CAPACITY='2'
$env:EMBERWAKE_WORKER_KEY=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
$env:EMBERWAKE_MATCH_DATA=Join-Path $root 'Backend/data/mobile-coordinator'
$runtime=Join-Path $root '.tools/dotnet/dotnet.exe'
$dll=Join-Path $root 'Backend/bin/Debug/net10.0/Emberwake.Backend.dll'
$adb='C:/Program Files/Unity/Hub/Editor/6000.2.6f2/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
$processes=@()
$logRoot=Join-Path $root ("test-logs/"+(Get-Date -Format "yyyyMMdd-HHmmss"));New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
if(Get-NetTCPConnection -State Listen -LocalPort 5080 -ErrorAction SilentlyContinue){throw 'Port 5080 is already in use. Stop the previous test server first.'}
try {
    $api=Start-Process $runtime -ArgumentList ('"'+$dll+'"') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logRoot "api.log") -RedirectStandardError (Join-Path $logRoot "api-errors.log");$processes+=$api
    $env:EMBERWAKE_COORDINATOR='http://127.0.0.1:5080'
    $env:EMBERWAKE_MATCH_EXECUTABLE=Join-Path $root 'Builds/Windows/Emberwake.exe'
    $env:EMBERWAKE_MATCH_DATA=Join-Path $root 'Backend/data/mobile-worker'
    $processes+=Start-Process $runtime -ArgumentList ('"'+$dll+'" --worker') -WindowStyle Hidden -PassThru
    $ready=$false
    for($i=0;$i -lt 30;$i++){try{if((Invoke-RestMethod 'http://127.0.0.1:5080/health').rankedAvailable){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 500}
    if(!$ready){throw 'Fusion coordinator/worker did not become ready. Check that port 5080 is free.'}
    if($InstallAndroid){
        & $adb get-state
        if($LASTEXITCODE -ne 0){throw 'Connect and authorize exactly one Android device with USB debugging enabled.'}
        & $adb install -r (Join-Path $root 'Builds/Android/Emberwake-preview.apk')
        if($LASTEXITCODE -ne 0){throw 'APK installation failed'}
        & $adb reverse tcp:5080 tcp:5080
        if($LASTEXITCODE -ne 0){throw 'USB port forwarding failed'}
        & $adb shell monkey -p com.emberwake.fleetduel -c android.intent.category.LAUNCHER 1
    }
    $processes+=Start-Process (Join-Path $root 'Builds/Windows/Emberwake.exe') -ArgumentList '-api http://127.0.0.1:5080' -PassThru
    if(!$Lan){Write-Host 'On Android: Settings > Service address > http://127.0.0.1:5080 > Reconnect. Keep USB attached. Complete/skip the tutorial, then press Battle on both devices. Combat uses Photon Fusion; USB forwards only the account API.'}
    if($Lan){
        Write-Host 'Manual APK / same-Wi-Fi test: use one of these PC addresses in Android Settings, then Reconnect. Use disposable test accounts on HTTP.'
        Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.IPAddress -match '^(192\.168\.|10\.|172\.(1[6-9]|2[0-9]|3[01])\.)'} | ForEach-Object {Write-Host ('http://'+$_.IPAddress+':5080')}
        Write-Host 'Allow the API through Windows Firewall on your private network if prompted. Do not port-forward this HTTP test service to the internet.'
    }
    Read-Host 'Press Enter here when finished to stop the test services'
} finally {
    foreach($process in $processes){if(!$process.HasExited){& taskkill.exe /PID $process.Id /T /F | Out-Null;$process.WaitForExit()}}
    if($InstallAndroid){& $adb reverse --remove tcp:5080}
}

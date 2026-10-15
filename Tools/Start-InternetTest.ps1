param([switch]$LaunchWindows)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$package=Join-Path $root 'Builds/Fusion-Windows-Worker-20261014'
$cloudflared=Join-Path $root '.tools/cloudflared/cloudflared.exe'
$config=Join-Path $root '.tools/cloudflared/royale.yml'
$logs=Join-Path $package ('test-logs/internet-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
if(!(Test-Path -LiteralPath $config)){throw 'Cloudflare tunnel configuration is missing.'}
$health=$null
try{$health=Invoke-RestMethod 'http://127.0.0.1:5080/health' -TimeoutSec 3}catch{}
if(!$health){
    $script=Join-Path $package 'Start-Server.ps1'
    Start-Process powershell.exe -ArgumentList @('-NoProfile','-File',('"'+$script+'"')) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logs 'api.log') -RedirectStandardError (Join-Path $logs 'api-errors.log') | Out-Null
    for($i=0;$i -lt 30;$i++){try{$health=Invoke-RestMethod 'http://127.0.0.1:5080/health' -TimeoutSec 2;break}catch{Start-Sleep -Milliseconds 500}}
}
if(!$health -or $health.transport -ne 'fusion' -or !$health.rankedAvailable){throw 'Local Fusion server is not ready. Check the server logs.'}
$existing=Get-CimInstance Win32_Process -Filter "Name = 'cloudflared.exe'" | Where-Object {$_.CommandLine -like '*royale.yml*'}
if(!$existing){
    Start-Process $cloudflared -ArgumentList @('tunnel','--config',('"'+$config+'"'),'run','bce26c82-6178-4d68-bbab-44a5e8083672') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logs 'tunnel.log') -RedirectStandardError (Join-Path $logs 'tunnel-errors.log') | Out-Null
}
$public=$null
for($i=0;$i -lt 15;$i++){try{$public=Invoke-RestMethod 'https://royale.inmomentservices.com/health' -TimeoutSec 4;break}catch{Start-Sleep -Seconds 1}}
if(!$public.rankedAvailable){throw 'Public HTTPS endpoint is not ready. Check the tunnel logs.'}
Write-Host 'Ready: https://royale.inmomentservices.com - Fusion battles enabled.'
Write-Host 'Keep this PC awake and online. The release app connects automatically; disable Wi-Fi on Android to test mobile data.'
if($LaunchWindows){Start-Process (Join-Path $root 'Builds/Release/Windows/Emberwake.exe') | Out-Null}

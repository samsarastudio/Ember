param([ValidateSet('us','usw','cae')][string]$Region='us',[ValidateRange(1,100)][int]$Capacity=4)
$ErrorActionPreference='Stop'
$serverRoot=$PSScriptRoot
$state=Join-Path $serverRoot 'state'
New-Item -ItemType Directory -Path $state -Force | Out-Null
$secretPath=Join-Path $state 'secrets.json'
if(!(Test-Path -LiteralPath $secretPath)){
    function New-Secret { $bytes=New-Object byte[] 32; $rng=[System.Security.Cryptography.RandomNumberGenerator]::Create(); try{$rng.GetBytes($bytes)}finally{$rng.Dispose()}; return -join ($bytes | ForEach-Object {$_.ToString('x2')}) }
    @{admin=(New-Secret);account=(New-Secret)} | ConvertTo-Json | Set-Content -LiteralPath $secretPath
}
$secrets=Get-Content -Raw -LiteralPath $secretPath | ConvertFrom-Json
$env:EMBERWAKE_ADMIN_TOKEN=$secrets.admin
$env:EMBERWAKE_ACCOUNT_KEY=$secrets.account
$env:EMBERWAKE_DB=Join-Path $state 'emberwake.db'
$env:EMBERWAKE_MATCH_DATA=Join-Path $state 'matches'
$env:EMBERWAKE_MATCH_EXECUTABLE=Join-Path $serverRoot 'Game/Emberwake.exe'
$env:EMBERWAKE_MATCH_REGION=$Region
$env:EMBERWAKE_MATCH_CAPACITY=$Capacity.ToString()
$env:ASPNETCORE_URLS='http://127.0.0.1:5080'
$env:EMBERWAKE_BATTLE_TRANSPORT='fusion'
$env:EMBERWAKE_REMOTE_WORKER='0'
Push-Location (Join-Path $serverRoot 'Api')
try { & './Emberwake.Backend.exe'; if($LASTEXITCODE -ne 0){throw "API exited with code $LASTEXITCODE"} } finally { Pop-Location }

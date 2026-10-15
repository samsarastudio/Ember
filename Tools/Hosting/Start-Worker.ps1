param([Parameter(Mandatory=$true)][string]$Coordinator,[string]$KeyFile=(Join-Path $PSScriptRoot 'worker-key.txt'))
$ErrorActionPreference='Stop'
if(!(Test-Path -LiteralPath $KeyFile)){throw 'Create worker-key.txt with the same EMBERWAKE_WORKER_KEY configured on the Pi. Keep it private.'}
$env:EMBERWAKE_WORKER_KEY=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $KeyFile)).Trim()
$env:EMBERWAKE_COORDINATOR=$Coordinator
$env:EMBERWAKE_MATCH_EXECUTABLE=Join-Path $PSScriptRoot 'Game/Emberwake.exe'
$env:EMBERWAKE_MATCH_DATA=Join-Path $PSScriptRoot 'worker-state'
& (Join-Path $PSScriptRoot 'Api/Emberwake.Backend.exe') --worker
if($LASTEXITCODE -ne 0){throw "Worker exited with code $LASTEXITCODE"}

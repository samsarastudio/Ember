param([string]$Destination)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(!$Destination){$Destination=Join-Path $taskRoot ('Builds/PiHosting-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if(Test-Path -LiteralPath $Destination){throw 'Choose a new destination; never overwrite a deployment or its data.'}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
& (Join-Path $taskRoot '.tools/dotnet/dotnet.exe') publish (Join-Path $taskRoot 'Backend/Emberwake.Backend.csproj') -c Release -r linux-arm64 --self-contained true -o (Join-Path $Destination 'Api') --nologo
if($LASTEXITCODE -ne 0){throw 'ARM64 publication failed'}
Copy-Item -Path (Join-Path $PSScriptRoot 'PiHosting/*') -Destination $Destination
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Hosting/Caddyfile') -Destination $Destination
Copy-Item -LiteralPath (Join-Path $taskRoot 'Docs/FUSION-SPLIT-HOSTING.md') -Destination (Join-Path $Destination 'README.md')
Write-Output "Raspberry Pi ARM64 package: $Destination"

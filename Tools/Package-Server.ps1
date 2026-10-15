param([string]$Destination)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(!$Destination){$Destination=Join-Path $taskRoot ('Builds/Hosting-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if(Test-Path -LiteralPath $Destination){throw 'Choose a new destination; hosting state must never be overwritten.'}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
& (Join-Path $taskRoot '.tools/dotnet/dotnet.exe') publish (Join-Path $taskRoot 'Backend/Emberwake.Backend.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $Destination 'Api') --nologo
if($LASTEXITCODE -ne 0){throw 'API publication failed'}
Copy-Item -LiteralPath (Join-Path $taskRoot 'Builds/Windows') -Destination (Join-Path $Destination 'Game') -Recurse
Copy-Item -Path (Join-Path $PSScriptRoot 'Hosting/*') -Destination $Destination
Copy-Item -LiteralPath (Join-Path $taskRoot 'Docs/MANUAL-HOSTING.md') -Destination (Join-Path $Destination 'ALL-ON-WINDOWS.md')
Copy-Item -LiteralPath (Join-Path $taskRoot 'Docs/FUSION-SPLIT-HOSTING.md') -Destination (Join-Path $Destination 'FUSION-SPLIT-HOSTING.md')
Copy-Item -LiteralPath (Join-Path $taskRoot 'Docs/FUSION-SPLIT-HOSTING.md') -Destination (Join-Path $Destination 'README.md')
Write-Output "Hosting package: $Destination"

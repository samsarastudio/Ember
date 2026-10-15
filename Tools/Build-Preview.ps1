param([ValidateSet('Windows','Android','WindowsRelease','AndroidRelease','IOS')][string]$Target='Windows',[ValidateRange(1,8)][int]$Jobs=2)
$ErrorActionPreference='Stop'
$env:BEE_BUILD_THREADS=$Jobs.ToString()
$env:EMBERWAKE_BUILD_JOBS=$Jobs.ToString()
$taskRoot=Split-Path $PSScriptRoot -Parent
$unity='C:/Program Files/Unity/Hub/Editor/6000.2.6f2/Editor/Unity.com'
if (!(Test-Path -LiteralPath $unity)) { throw 'Install the matching Unity editor with the target build module.' }
& $unity -batchmode -nographics -job-worker-count $Jobs -diag-debug-shader-compiler -projectPath (Join-Path $taskRoot 'Client') -executeMethod "BuildGame.$Target" -quit -logFile (Join-Path $taskRoot "unity-$Target.log")
if ($LASTEXITCODE -ne 0) { throw "Unity build failed. See unity-$Target.log." }

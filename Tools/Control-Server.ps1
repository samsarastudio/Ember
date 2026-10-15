param([ValidateSet('start','stop','restart')][string]$Action='start')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$package=Join-Path $root 'Builds/Fusion-Windows-Worker-20261014'
if($Action -in @('stop','restart')){
    $expected=[IO.Path]::GetFullPath((Join-Path $package 'Api/Emberwake.Backend.exe'))
    $processes=Get-CimInstance Win32_Process -Filter "Name='Emberwake.Backend.exe'" | Where-Object {$_.ExecutablePath -eq $expected}
    foreach($process in $processes){
        # Terminate only this package's API and its battle children, never unrelated servers.
        $owned=[Diagnostics.Process]::GetProcessById($process.ProcessId)
        # Windows PowerShell lacks Process.Kill(entireProcessTree); taskkill uses a verified PID.
        & taskkill.exe /PID $owned.Id /T /F | Out-Null
        $owned.WaitForExit(10000) | Out-Null
    }
    Write-Output 'Emberwake server stopped. Cloudflare tunnel remains connected.'
}
if($Action -in @('start','restart')){& (Join-Path $PSScriptRoot 'Start-InternetTest.ps1')}

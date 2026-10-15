param([ValidateSet('us','usw','cae')][string]$Region='us')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$probe=Join-Path $taskRoot 'Builds/FusionProbe/FusionProbe.exe'
if (!(Test-Path -LiteralPath $probe)) { throw 'Build the probe with Unity executeMethod BuildFusionProbe.Windows first.' }
$session='emberwake-probe-'+[Guid]::NewGuid().ToString('N')
$logs=Join-Path $taskRoot ('Builds/FusionProbe/Logs/'+$session)
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$processes=@()
try {
    foreach ($role in @('server','client1','client2')) {
        $mode=if ($role -eq 'server') {'server'} else {'client'}
        $log=Join-Path $logs ($role+'.log')
        $arguments=@('-batchmode','-nographics','-fusionRole',$mode,'-fusionRegion',$Region,'-fusionSession',$session,'-logFile',('"'+$log+'"'))
        $processes+=Start-Process -FilePath $probe -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if ($role -eq 'server') {
            $deadline=(Get-Date).AddSeconds(60)
            do {
                Start-Sleep -Milliseconds 500
                if ($processes[0].HasExited) { throw "Server exited early. See $log" }
                $ready=(Test-Path -LiteralPath $log) -and (Select-String -LiteralPath $log -Pattern 'FUSION_PROBE_CONNECTED' -Quiet)
            } until ($ready -or (Get-Date) -gt $deadline)
            if (!$ready) { throw "Server connection timed out. See $log" }
        }
    }
    foreach ($process in $processes) {
        if (!$process.WaitForExit(120000)) { throw "Probe timed out. See $logs" }
        if ($process.ExitCode -ne 0) { throw "Probe failed. See $logs" }
    }
    foreach ($role in @('server','client1','client2')) {
        if (!(Select-String -LiteralPath (Join-Path $logs ($role+'.log')) -Pattern 'FUSION_PROBE_TWO_PLAYERS_OK' -Quiet)) { throw "$role did not confirm two players. See $logs" }
    }
    Write-Output "PASS: dedicated server and two clients connected in $Region. Logs: $logs"
} finally {
    foreach ($process in $processes) { if (!$process.HasExited) { Stop-Process -Id $process.Id } }
}

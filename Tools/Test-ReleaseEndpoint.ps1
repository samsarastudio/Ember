# Uses the live guest service and two disposable test clients. Never stops the host.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$logs=Join-Path $root ('Builds/ReleaseVerification/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$clients=@()
try {
    $clients+=Start-Process (Join-Path $root 'Builds/Release/Windows/Emberwake.exe') -ArgumentList @('-batchmode','-ranked-autotest','-fusion-autotest','-api','http://127.0.0.1:1','-logFile',('"'+(Join-Path $logs 'release.log')+'"')) -WindowStyle Hidden -PassThru
    # Allow the release client to load its saved captain before the dev peer signs in.
    Start-Sleep -Seconds 10
    $account=Invoke-RestMethod 'https://royale.inmomentservices.com/v1/auth/guest' -Method Post -ContentType 'application/json' -Body '{"name":"Release Test Peer"}'
    $clients+=Start-Process (Join-Path $root 'Builds/Windows/Emberwake.exe') -ArgumentList @('-batchmode','-ranked-autotest','-fusion-autotest','-api','https://royale.inmomentservices.com','-test-credential',$account.credential,'-logFile',('"'+(Join-Path $logs 'peer.log')+'"')) -WindowStyle Hidden -PassThru
    Write-Output "Release verification logs: $logs"
    $deadline=(Get-Date).AddSeconds(180)
    while((Get-Date) -lt $deadline -and @($clients | Where-Object {!$_.HasExited}).Count){Start-Sleep -Milliseconds 500}
    foreach($client in $clients){if(!$client.HasExited -or $client.ExitCode -ne 0){throw 'Release test failed or timed out'}}
    $results=@()
    foreach($name in @('release','peer')){
        $match=Select-String -LiteralPath (Join-Path $logs ($name+'.log')) -Pattern 'FUSION_CLIENT_RESULT team=(\d+) tick=(\d+) winner=(-?\d+) hash=(-?\d+).*pass=True'
        if(!$match){throw 'Missing successful combat result'}
        $results+=$match.Matches[0]
    }
    if($results[0].Groups[1].Value -eq $results[1].Groups[1].Value){throw 'Same team assigned'}
    foreach($field in 2..4){if($results[0].Groups[$field].Value -ne $results[1].Groups[$field].Value){throw 'Final states differ'}}
    Write-Output 'PASS: release ignored invalid URL override, joined live Fusion match, and agreed with peer.'
} finally {
    foreach($client in $clients){if(!$client.HasExited){$client.Kill($true);$client.WaitForExit()}}
}

. "$PSScriptRoot/common.ps1"
$exe=Join-Path $Artifacts 'windows/Foundry.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Build the Windows client first: tools/build.ps1' }
$processes=@()
try {
    $serverLog=Join-Path $Artifacts 'smoke-server.log'
    $server=Start-Process -FilePath $exe -ArgumentList @('-batchmode','-nographics','--server','--smoke','--port','17777','--quit-after','26','-logFile',('"'+$serverLog+'"')) -WindowStyle Hidden -PassThru
    $processes+=$server
    Start-Sleep -Seconds 2
    foreach ($name in @('Smoke-A','Smoke-B')) {
        $log=Join-Path $Artifacts "$name.log"
        $processes+=Start-Process -FilePath $exe -ArgumentList @('--connect','127.0.0.1','--port','17777','--smoke','--name',$name,'--quit-after','20','-screen-fullscreen','0','-screen-width','640','-screen-height','480','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    }
    foreach ($p in $processes) {
        if (!$p.WaitForExit(45000)) { throw "Game process $($p.Id) timed out" }
        if ($p.ExitCode -ne 0) { throw "Game process $($p.Id) failed with exit $($p.ExitCode)" }
    }
    $results=@()
    foreach ($logName in @('smoke-server.log','Smoke-A.log','Smoke-B.log')) {
        $text=Get-Content -LiteralPath (Join-Path $Artifacts $logName) -Raw
        if ($text -match 'Exception:|NullReferenceException|error CS\d+') { throw "Runtime errors in $logName" }
        $match=[regex]::Match($text,'FOUNDRY_SMOKE_RESULT (\{[^\r\n]+\})')
        if (!$match.Success) { throw "Missing smoke result in $logName" }
        $result=$match.Groups[1].Value | ConvertFrom-Json
        if ($result.peakPlayers -lt 2) { throw "$logName never replicated two players" }
        $results+=$result
    }
    if (($results | Measure-Object kills -Maximum).Maximum -lt 1 -or ($results | Measure-Object deaths -Maximum).Maximum -lt 1) { throw 'No validated combat observed' }
    $results | ConvertTo-Json | Set-Content (Join-Path $Artifacts 'smoke-results.json')
    Write-Output 'PASS: dedicated server, two replicated clients, authoritative combat, and clean process exits.'
} finally {
    foreach ($p in $processes) { if (!$p.HasExited) { Stop-Process -Id $p.Id } }
}

. "$PSScriptRoot/common.ps1"
$exe=Join-Path $Artifacts 'windows/Foundry.exe'
if (!(Test-Path -LiteralPath $exe)) { & "$PSScriptRoot/build.ps1" }
if (!(Test-Path -LiteralPath $exe)) { throw 'Windows game build is missing.' }
$server=Start-Process -FilePath $exe -ArgumentList @('-batchmode','-nographics','--server','--port','7777','-logFile',('"'+(Join-Path $Artifacts 'game-server.log')+'"')) -WindowStyle Hidden -PassThru
$server.Id | Set-Content (Join-Path $Artifacts 'game-server.pid')
Start-Sleep -Seconds 2
foreach ($name in @('Operator-A','Operator-B')) {
    Start-Process -FilePath $exe -ArgumentList @('--connect','127.0.0.1','--port','7777','--name',$name,'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"'+(Join-Path $Artifacts "$name.log")+'"')) -WindowStyle Normal
}
Write-Output 'Dedicated server and two clients started. Close both clients and stop the dedicated server using its saved PID when finished.'

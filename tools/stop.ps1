. "$PSScriptRoot/common.ps1"
$pidFile=Join-Path $Artifacts 'lobby.pid'
if (Test-Path -LiteralPath $pidFile) {
    $lobbyPid=[int](Get-Content -LiteralPath $pidFile)
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$lobbyPid" -ErrorAction SilentlyContinue
    $expected=Join-Path $Artifacts 'lobby/Foundry.Lobby.dll'
    if ($process -and $process.CommandLine.Contains($expected)) { Stop-Process -Id $lobbyPid }
}

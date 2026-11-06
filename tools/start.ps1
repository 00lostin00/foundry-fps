. "$PSScriptRoot/common.ps1"
New-Item -ItemType Directory -Path $Artifacts -Force | Out-Null
$health=$null
try { $health=Invoke-RestMethod 'http://127.0.0.1:5080/health' -TimeoutSec 2 } catch { }
if ($health) {
    if ($health.version -eq '1.0.0') { Write-Output 'Lobby already running: http://127.0.0.1:5080'; exit 0 }
    throw 'Port 5080 is occupied by a different service.'
}
dotnet publish (Join-Path $ProjectRoot 'server/Foundry.Lobby.csproj') -c Release -o (Join-Path $Artifacts 'lobby') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Lobby build failed' }
$dll=Join-Path $Artifacts 'lobby/Foundry.Lobby.dll'
$p=Start-Process -FilePath 'dotnet' -ArgumentList ('"'+$dll+'"') -WorkingDirectory $ProjectRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $Artifacts 'lobby.stdout.log') -RedirectStandardError (Join-Path $Artifacts 'lobby.stderr.log')
$p.Id | Set-Content (Join-Path $Artifacts 'lobby.pid')
Wait-LocalLobby
Write-Output 'Lobby ready: http://127.0.0.1:5080'

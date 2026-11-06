. "$PSScriptRoot/common.ps1"
dotnet build (Join-Path $ProjectRoot 'server/Foundry.Lobby.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Lobby compilation failed' }
dotnet run --project (Join-Path $ProjectRoot 'tests/Rules/Rules.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Weapon rule checks failed' }
python (Join-Path $ProjectRoot 'tests/lobby_checks.py')
if ($LASTEXITCODE -ne 0) { throw 'Lobby integration checks failed' }

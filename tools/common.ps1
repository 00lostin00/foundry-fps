$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$UnityProject = Join-Path $ProjectRoot 'UnityProject'
$Artifacts = Join-Path $ProjectRoot 'artifacts'
function Find-UnityEditor {
    if ($env:UNITY_EDITOR -and (Test-Path -LiteralPath $env:UNITY_EDITOR)) { return $env:UNITY_EDITOR }
    $roots = @('C:\Program Files\Unity\Hub\Editor','D:\Unity\Hub\Editor','D:\Unity', (Join-Path $ProjectRoot '.tools\Unity'))
    $editorsFile = Join-Path $env:APPDATA 'UnityHub\editors.json'
    $secondaryFile = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path -LiteralPath $secondaryFile) {
        try {
            $secondary = Get-Content -LiteralPath $secondaryFile -Raw | ConvertFrom-Json
            if ($secondary -and (Test-Path -LiteralPath $secondary)) {
                $roots += Get-ChildItem -LiteralPath $secondary -Directory | Where-Object Name -match '^6000\.' | Select-Object -ExpandProperty FullName
            }
        } catch { Write-Verbose 'No secondary editor location.' }
    }
    if (Test-Path -LiteralPath $editorsFile) {
        try {
            $data = Get-Content -LiteralPath $editorsFile -Raw | ConvertFrom-Json
            foreach ($entry in $data) {
                if ($entry.location) { $roots += [string]$entry.location }
            }
        } catch { Write-Verbose 'No supported editor metadata in Hub.' }
    }
    foreach ($root in $roots) {
        if (!(Test-Path -LiteralPath $root)) { continue }
        $direct = Join-Path $root 'Editor\Unity.exe'
        if (Test-Path -LiteralPath $direct) { return $direct }
        $found = Get-ChildItem -LiteralPath $root -Filter Unity.exe -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object FullName -match '\\Editor\\Unity.exe$' | Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    throw 'Unity Editor not found. In Unity Hub install Unity 6.3 LTS, activate Personal, then retry. Or set UNITY_EDITOR to the full Editor/Unity.exe path.'
}
function Wait-LocalLobby {
    for ($i=0; $i -lt 60; $i++) {
        try {
            $health = Invoke-RestMethod 'http://127.0.0.1:5080/health' -TimeoutSec 1
            if ($health.version -eq '1.0.0') { return }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw 'Lobby did not become healthy within 30 seconds; inspect artifacts/lobby.stderr.log.'
}

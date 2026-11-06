param([ValidateSet('Windows','Linux')] [string]$Target='Windows')
. "$PSScriptRoot/common.ps1"
$editor = Find-UnityEditor
New-Item -ItemType Directory -Path $Artifacts -Force | Out-Null
$log = Join-Path $Artifacts "build-$Target.log"
$method = "FoundryBuild.$Target"
$process = Start-Process -FilePath $editor -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',$UnityProject,'-executeMethod',$method,'-logFile',$log) -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity build failed (exit $($process.ExitCode)). Read $log" }
Write-Output "Build passed. Output: $Artifacts"

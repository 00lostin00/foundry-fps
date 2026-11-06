. "$PSScriptRoot/common.ps1"
$editor = Find-UnityEditor
Start-Process -FilePath $editor -ArgumentList @('-projectPath', ('"' + $UnityProject + '"')) -WindowStyle Normal

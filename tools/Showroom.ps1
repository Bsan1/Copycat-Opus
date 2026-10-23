# Builds the model showroom scene (Assets/Opus/Scenes/ModelShowroom.unity) and captures its editor view into
# the Builds/Shots/model_showroom folder. The scene is inspected in the editor without pressing Play.
# Usage: powershell -ExecutionPolicy Bypass -File tools/Showroom.ps1 [-OnlyCapture]
param([string]$UnityPath, [switch]$OnlyCapture)
. (Join-Path $PSScriptRoot '_common.ps1')

$unity = Get-UnityEditorPath $UnityPath
Assert-ProjectClosed

if (-not $OnlyCapture) {
    $code = Invoke-Unity -Unity $unity -Arguments @('-quit', '-executeMethod', 'Opus.Editor.ModelShowroomScene.Create') -LogName 'unity-showroom.log'
    if ($code -ne 0) { throw 'Could not build the showroom scene; see Logs\unity-showroom.log.' }
}
$code = Invoke-Unity -Unity $unity -Arguments @('-quit', '-executeMethod', 'Opus.Editor.ModelShowroomScene.Capture') -LogName 'unity-showroom-capture.log'
if ($code -ne 0) { throw 'Could not capture the showroom shots; see Logs\unity-showroom-capture.log.' }
Write-Host ('Shots: ' + (Join-Path $ProjectRoot 'Builds\Shots\model_showroom'))

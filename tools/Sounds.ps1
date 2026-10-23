# Writes all sounds synthesised from code as Builds/Sounds/*.wav (the editor does not open).
# Usage: powershell -ExecutionPolicy Bypass -File tools/Sounds.ps1
param([string]$UnityPath)
. (Join-Path $PSScriptRoot '_common.ps1')

$unity = Get-UnityEditorPath $UnityPath
Assert-ProjectClosed
$code = Invoke-Unity -Unity $unity -Arguments @('-quit', '-executeMethod', 'Opus.Editor.SoundExport.Export') -LogName 'unity-sounds.log'
if ($code -ne 0) { throw 'Sound export failed; see Logs\unity-sounds.log.' }
Write-Host ('Sounds: ' + (Join-Path $ProjectRoot 'Builds\Sounds'))

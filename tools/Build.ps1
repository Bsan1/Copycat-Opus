# Configures the project and builds the Windows exe (the editor does not open).
# Usage: powershell -ExecutionPolicy Bypass -File tools/Build.ps1
param([string]$UnityPath)
. (Join-Path $PSScriptRoot '_common.ps1')

$unity = Get-UnityEditorPath $UnityPath
Assert-ProjectClosed

# Configuration and build are separate runs because changing the input setting needs a restart.
$code = Invoke-Unity -Unity $unity -Arguments @('-quit', '-executeMethod', 'Opus.Editor.Setup.ConfigureProject') -LogName 'unity-configure.log'
if ($code -ne 0) { throw 'Configuration failed; see Logs\unity-configure.log.' }

$code = Invoke-Unity -Unity $unity -Arguments @('-quit', '-executeMethod', 'Opus.Editor.Build.Windows') -LogName 'unity-build.log'
if ($code -ne 0) { throw 'Build failed; see Logs\unity-build.log.' }

Write-Host ('Exe: ' + (Join-Path $ProjectRoot 'Builds\Windows\CopycatOpus.exe'))

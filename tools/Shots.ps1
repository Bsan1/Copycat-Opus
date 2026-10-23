# Plays a scenario in the built exe and takes screenshots including the interface; no window opens on screen.
# Usage: powershell -ExecutionPolicy Bypass -File tools/Shots.ps1 [-Scenario u0_overview] [-Width 1920] [-Height 1080]
#
# Since Unity does not render cameras and uGUI canvases in batchmode, the game runs in normal mode;
# its window is embedded in a parent window created here and never shown (-parentHWND).
param(
    [string]$Scenario = 'u0_overview',
    [int]$Width = 1920,
    [int]$Height = 1080,
    [int]$TimeoutSeconds = 120
)
. (Join-Path $PSScriptRoot '_common.ps1')
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$exe = Join-Path $ProjectRoot 'Builds\Windows\CopycatOpus.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the exe with tools/Build.ps1 first.' }

$out = Join-Path $ProjectRoot "Builds\Shots\$Scenario"
New-Item -ItemType Directory -Path $out -Force | Out-Null
Get-ChildItem -LiteralPath $out | Remove-Item -Recurse -Force

# Parent window: Unity stops rendering in an invisible window, so it is "visible" but far off the screen.
$host_ = New-Object System.Windows.Forms.Form
$host_.FormBorderStyle = 'None'
$host_.ShowInTaskbar = $false
$host_.StartPosition = 'Manual'
$host_.Location = New-Object System.Drawing.Point(-30000, -30000)
$host_.ClientSize = New-Object System.Drawing.Size($Width, $Height)
$host_.Show()
$host_.Location = New-Object System.Drawing.Point(-30000, -30000)
$parent = $host_.Handle.ToInt64()

$playerArgs = @(
    '-parentHWND', $parent, '-screen-width', $Width, '-screen-height', $Height,
    '-scenario', $Scenario, '-shots', $out, '-shot-size', "${Width}x${Height}",
    '-logFile', (Join-Path $out 'player.log'))
$argLine = ($playerArgs | ForEach-Object { Format-Arg "$_" }) -join ' '

$watch = [Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $exe -ArgumentList $argLine -WorkingDirectory $ProjectRoot -PassThru
$null = $process.Handle
try {
    # The embedded window sends messages to the parent window; if they are not processed the game hangs.
    while (-not $process.HasExited) {
        [System.Windows.Forms.Application]::DoEvents()
        if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
            Stop-Process -Id $process.Id -Force
            throw "The scenario timed out; $out\player.log"
        }
        Start-Sleep -Milliseconds 15
    }
}
finally {
    $host_.Dispose()
}
$process.WaitForExit()

$resultPath = Join-Path $out 'result.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw "No report (exit code $($process.ExitCode)); $out\player.log" }
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json

$status = if ($result.passed) { 'PASSED' } else { 'FAILED' }
Write-Host ('Scenario {0}: {1} | {2} | window {3}x{4} | avg. frame {5:N2} ms (CPU main thread {6:N2} ms, {7:N0} batches) | {8:N0} s' -f `
        $result.scenario, $status, $result.graphicsDevice, $result.screenWidth, $result.screenHeight, $result.averageFrameMs,
        $result.averageCpuMainMs, $result.averageBatches, $watch.Elapsed.TotalSeconds)
foreach ($shot in $result.shots) { Write-Host ('  {0}  (luminance deviation {1:N1})  {2}' -f $shot.name, $shot.luminanceDeviation, $shot.file) }
foreach ($e in $result.errors) { Write-Host "  ERROR: $e" }
foreach ($w in $result.warnings) { Write-Host "  warning: $w" }
if (-not $result.passed) { exit 1 }

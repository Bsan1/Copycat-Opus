# Runs the Unity tests without opening the editor.
# Usage: powershell -ExecutionPolicy Bypass -File tools/Test.ps1 [-Platform EditMode|PlayMode]
param(
    [string]$UnityPath,
    [ValidateSet('EditMode', 'PlayMode')] [string]$Platform = 'EditMode'
)
. (Join-Path $PSScriptRoot '_common.ps1')

$unity = Get-UnityEditorPath $UnityPath
Assert-ProjectClosed

$results = Join-Path $LogRoot "tests-$Platform.xml"
if (Test-Path -LiteralPath $results) { Remove-Item -LiteralPath $results }

$code = Invoke-Unity -Unity $unity -Arguments @('-runTests', '-testPlatform', $Platform, '-testResults', $results) -LogName "unity-tests-$Platform.log"
if (-not (Test-Path -LiteralPath $results)) { throw "No test result was produced (exit code $code); Logs\unity-tests-$Platform.log" }

[xml]$xml = Get-Content -LiteralPath $results -Raw -Encoding UTF8
$run = $xml.'test-run'
Write-Host ('Tests: {0} total, {1} passed, {2} failed, {3} skipped' -f $run.total, $run.passed, $run.failed, $run.skipped)
foreach ($case in $xml.SelectNodes("//test-case[@result='Failed']")) {
    $message = $case.SelectSingleNode('failure/message')
    Write-Host ('  FAILED: ' + $case.fullname)
    if ($message) { Write-Host ('    ' + $message.InnerText.Trim()) }
}
if ([int]$run.failed -gt 0 -or $code -ne 0) { exit 1 }

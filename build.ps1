param([switch]$Stage)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The existing .NET Framework C# compiler was not found. This project does not install dependencies.'
}
Push-Location -LiteralPath $PSScriptRoot
try {
    $appOutput = if ($Stage) { 'Pawquilt.next.exe' } else { 'Pawquilt.exe' }
    $testOutput = if ($Stage) { 'PawquiltTests.next.exe' } else { 'PawquiltTests.exe' }
    $sources = @('Program.cs', 'Motion.cs', 'Native.cs', 'Companion.cs', 'Tests.cs', 'Preflight.cs', 'Diagnostics.cs', 'Interaction.cs', 'SpriteArt.cs', 'TimingAdapter.cs', 'MenuTheme.cs', 'StartupLink.cs', 'LocalBehavior.cs', 'SpeechBubble.cs', 'BehaviorTests.cs')
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /main:Qa3moz.Program /win32manifest:app.manifest ("/out:" + $appOutput) /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'App compilation failed' }
    & $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /main:Qa3moz.TestProgram ("/out:" + $testOutput) /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    & (Join-Path $PSScriptRoot $testOutput)
    if ($LASTEXITCODE -ne 0) { throw 'Headless tests failed' }
    Write-Output "Built successfully: $appOutput / $testOutput. No overlay was launched."
} finally { Pop-Location }

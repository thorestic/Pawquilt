param([switch]$Stage, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The existing .NET Framework C# compiler was not found. This project does not install dependencies.'
}
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $projectRoot 'bin'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Push-Location -LiteralPath $projectRoot
try {
    $appOutput = Join-Path $outputRoot $(if ($Stage) { 'Pawquilt.next.exe' } else { 'Pawquilt.exe' })
    $testOutput = Join-Path $outputRoot $(if ($Stage) { 'PawquiltTests.next.exe' } else { 'PawquiltTests.exe' })
    $sources = @('src', 'tests' | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $projectRoot $_) -File -Filter '*.cs' | ForEach-Object { $_.FullName }
    })
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /main:Qa3moz.Program /win32manifest:app.manifest ("/out:" + $appOutput) /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'App compilation failed' }
    & $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /main:Qa3moz.TestProgram ("/out:" + $testOutput) /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    # Runtime artwork must remain adjacent to the output executable.
    $assetOutput = Join-Path $outputRoot 'assets'
    New-Item -ItemType Directory -Path $assetOutput -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'assets') -Force | Copy-Item -Destination $assetOutput -Recurse -Force
    if (-not $SkipTests) {
        & $testOutput
        if ($LASTEXITCODE -ne 0) { throw 'Headless tests failed' }
    }
    Write-Output "Built successfully: $appOutput / $testOutput. No overlay was launched."
} finally { Pop-Location }

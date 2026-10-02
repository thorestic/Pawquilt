$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Installed .NET Framework x64 compiler unavailable. No dependencies are installed by this script.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $projectRoot 'bin'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Push-Location -LiteralPath $projectRoot
try {
    $sources = @('src', 'tests' | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $projectRoot $_) -File -Filter '*.cs' | ForEach-Object { $_.FullName }
    })
    $testOutput = Join-Path $outputRoot 'PawquiltSixSmoke.exe'
    & $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /main:Qa3moz.SixMoveSmoke ("/out:" + $testOutput) /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Focused-check compilation failed' }
    & $testOutput (Join-Path $projectRoot 'assets\pixel')
    if ($LASTEXITCODE -ne 0) { throw 'Six-move integration checks failed' }
} finally { Pop-Location }

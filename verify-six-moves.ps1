$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Installed .NET Framework x64 compiler unavailable. No dependencies are installed by this script.' }
Push-Location -LiteralPath $PSScriptRoot
try {
    $sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.cs' | ForEach-Object { $_.Name })
    & $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /main:Qa3moz.SixMoveSmoke /out:PawquiltSixSmoke.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Focused-check compilation failed' }
    & .\PawquiltSixSmoke.exe .\assets\pixel
    if ($LASTEXITCODE -ne 0) { throw 'Six-move integration checks failed' }
} finally { Pop-Location }

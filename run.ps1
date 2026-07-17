$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\DesktopWidgets.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $PSScriptRoot 'build.ps1')
}
Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot

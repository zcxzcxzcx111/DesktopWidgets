$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    throw '未找到项目内 .NET SDK，请先安装到 .tools\dotnet。'
}
& $dotnet build $PSScriptRoot -c Release

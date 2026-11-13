# Crea un unico Pinboard.exe in dist\ (non serve installare .NET per usarlo).
$ErrorActionPreference = "Stop"

dotnet publish "$PSScriptRoot\src\Pinboard" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o "$PSScriptRoot\dist"

Write-Host ""
Write-Host "Eseguibile pronto: $PSScriptRoot\dist\Pinboard.exe"

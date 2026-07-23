# Builds a self-contained, single-file Windows exe into .\publish
# Usage:  .\publish.ps1
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

dotnet publish TaskFirst.csproj -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o publish

Write-Host ""
Write-Host "Published to: $(Join-Path $PSScriptRoot 'publish\TaskFirst.exe')" -ForegroundColor Green

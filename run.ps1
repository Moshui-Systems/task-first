# Quick launcher for TaskFirst.
# Usage:  .\run.ps1          -> build + run with settings window
#         .\run.ps1 -Tray    -> build + run minimized to the system tray
param([switch]$Tray)

$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

dotnet build -v quiet -nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

if ($Tray) {
    dotnet run --no-build -- --tray
} else {
    dotnet run --no-build
}

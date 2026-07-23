# Removes TaskFirst: stops it, deletes the auto-start task, shortcut, and installed files.
# Self-elevates. Your config/license in %AppData%\TaskFirst is left intact.

$ErrorActionPreference = "SilentlyContinue"

$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe -Verb RunAs `
        -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`""
    exit
}

$TaskName   = "TaskFirst"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\TaskFirst"

Get-Process -Name "TaskFirst" | Stop-Process -Force
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
Remove-Item (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\TaskFirst.lnk") -Force
Remove-Item $InstallDir -Recurse -Force

Write-Host "TaskFirst uninstalled. (Settings in %AppData%\TaskFirst were kept.)" -ForegroundColor Green

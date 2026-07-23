# TaskFirst installer — builds, installs to a stable location, and sets up
# elevated auto-start at logon (Scheduled Task, highest privileges).
#
#   Right-click → "Run with PowerShell", or:  powershell -ExecutionPolicy Bypass -File .\install.ps1
#
# Self-elevates (one UAC prompt), so you don't need to open an admin shell yourself.

$ErrorActionPreference = "Stop"

# --- self-elevate ---
$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Requesting administrator rights..." -ForegroundColor Yellow
    Start-Process powershell.exe -Verb RunAs `
        -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`""
    exit
}

Set-Location -Path $PSScriptRoot

$TaskName   = "TaskFirst"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\TaskFirst"
$ExeName    = "TaskFirst.exe"
$ExePath    = Join-Path $InstallDir $ExeName

Write-Host "Building self-contained TaskFirst..." -ForegroundColor Cyan
dotnet publish TaskFirst.csproj -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o publish | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# --- stop any running instance, then copy ---
Get-Process -Name "TaskFirst" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path "publish\$ExeName" -Destination $ExePath -Force
Write-Host "Installed to $ExePath" -ForegroundColor Green

# --- scheduled task: run at logon, elevated, no per-login UAC prompt ---
$action    = New-ScheduledTaskAction  -Execute $ExePath -Argument "--tray"
$trigger   = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
$settings  = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::Zero)
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Principal $principal -Settings $settings -Force | Out-Null
Write-Host "Auto-start enabled (Scheduled Task '$TaskName', highest privileges)." -ForegroundColor Green

# --- Start Menu shortcut ---
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\TaskFirst.lnk"
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($startMenu)
$sc.TargetPath = $ExePath
$sc.WorkingDirectory = $InstallDir
$sc.Description = "TaskFirst"
$sc.Save()

# --- launch now (already elevated) ---
Start-Process -FilePath $ExePath -ArgumentList "--tray"
Write-Host "`nTaskFirst is installed and running in the system tray." -ForegroundColor Green
Write-Host "It will start automatically (as admin) every time you log in." -ForegroundColor Green
Write-Host "Uninstall any time with:  .\uninstall.ps1" -ForegroundColor DarkGray

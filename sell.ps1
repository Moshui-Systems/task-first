# Mint a Pro license key for a buyer and copy it to the clipboard.
# Usage:  .\sell.ps1 buyer@example.com            (perpetual)
#         .\sell.ps1 buyer@example.com -Days 365  (1-year subscription key)
#
# On each Stripe sale: grab the buyer's email, run this, paste the key into your reply.

param(
    [Parameter(Mandatory = $true)][string]$Email,
    [int]$Days = 0
)

$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

$keyFile = Join-Path $env:USERPROFILE ".taskfirst-keys\private.key"
if (-not (Test-Path $keyFile)) {
    throw "Private key not found at $keyFile.`nGenerate one with: dotnet run --project tools/LicenseTool -- keygen"
}

$toolArgs = @('run', '--project', 'tools/LicenseTool', '--', 'issue', $Email, '--key-file', $keyFile)
if ($Days -gt 0) { $toolArgs += @('--days', "$Days") }

$out = & dotnet @toolArgs
$out | Write-Output

$token = ($out | Where-Object { $_ -match '^ey' } | Select-Object -First 1)
if ($token) {
    $token.Trim() | Set-Clipboard
    Write-Host "`n✓ License key copied to clipboard — paste it into the buyer's email." -ForegroundColor Green
} else {
    Write-Host "`n! Could not find the key in the output above." -ForegroundColor Yellow
}

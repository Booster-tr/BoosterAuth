# Booster Auth setup script (Windows PowerShell 5.1+ / PowerShell 7).
# Asks for the addresses, generates random passwords and keys, and writes the .env file.
# Usage:  powershell -ExecutionPolicy Bypass -File .\setup.ps1
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

if (Test-Path .env) {
    Write-Host ".env already exists and was not overwritten. To reinstall, back up and delete .env first." -ForegroundColor Yellow
    exit 1
}

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
function RandomBytes([int]$n) { $b = New-Object byte[] $n; $rng.GetBytes($b); return ,$b }
function Hex([int]$n) { $b = RandomBytes $n; ($b | ForEach-Object { $_.ToString('x2') }) -join '' }
function B64([int]$n) { $b = RandomBytes $n; [Convert]::ToBase64String([byte[]]$b) }
function AdminPassword {
    $chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
    $b = RandomBytes 16
    $p = -join ($b | ForEach-Object { $chars[$_ % $chars.Length] })
    return "$p-A7k!"   # guarantees upper/lower case, digit and symbol
}
function Ask([string]$question, [string]$default) {
    $a = Read-Host "$question [$default]"
    if ([string]::IsNullOrWhiteSpace($a)) { return $default } else { return $a.Trim() }
}

Write-Host ""
Write-Host "Booster Auth setup" -ForegroundColor Cyan
Write-Host "The Booster Auth address must be one of the domains in your license (e.g. auth.company.com)."
Write-Host "For a local trial you can add a '127.0.0.1 <address>' line to the hosts file."
Write-Host ""

$publicHost = Ask "Booster Auth address (host)" "auth.company.com"
$publicHost = ($publicHost -replace '^https?://', '').TrimEnd('/')
$httpsPort  = Ask "HTTPS port" "443"
$publicUrl  = if ($httpsPort -eq '443') { "https://$publicHost" } else { "https://${publicHost}:$httpsPort" }
$panelPort  = Ask "Management panel port" "3000"
$panelUrl   = Ask "Management panel address" "http://localhost:$panelPort"
$adminEmail = Ask "Administrator e-mail" "admin@booster.local"
$adminPass  = AdminPassword
$o2Pass     = "Bo-9" + (Hex 16)   # OpenObserve guclu parola ister (buyuk + kucuk harf, rakam, ozel karakter)

$values = [ordered]@{
    PUBLIC_HOST              = $publicHost
    GATEWAY_HTTPS_PORT       = $httpsPort
    PUBLIC_URL               = $publicUrl
    PANEL_PORT               = $panelPort
    PANEL_PUBLIC_URL         = $panelUrl.TrimEnd('/')
    BOOTSTRAP_ADMIN_EMAIL    = $(if ($adminEmail -eq 'admin@booster.local') { '' } else { $adminEmail })
    BOOTSTRAP_ADMIN_PASSWORD = $adminPass
    POSTGRES_PASSWORD        = Hex 24
    REDIS_PASSWORD           = Hex 24
    OPENFGA_PRESHARED_KEY    = Hex 24
    HASHING_PEPPER           = Hex 32
    HASHING_ENCRYPTION_KEY   = B64 32
    IDP_APPLICATION_PEPPER   = Hex 32
    PANEL_SESSION_SECRET     = Hex 32
    OPENOBSERVE_ADMIN_PASSWORD = $o2Pass
    OTEL_HEADERS             = "Authorization=Basic " + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("admin@booster.local:$o2Pass"))
}

$lines = Get-Content .env.example
$out = foreach ($line in $lines) {
    if ($line -match '^([A-Z0-9_]+)=') {
        $key = $Matches[1]
        if ($values.Contains($key)) { "$key=$($values[$key])"; continue }
    }
    $line
}
# UTF-8 (no BOM) and LF: docker compose reads both; no BOM makes moving the file to Linux easier.
[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot '.env'), (($out -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding $false))

foreach ($d in 'data', 'pki', 'secrets') { New-Item -ItemType Directory -Force -Path $d | Out-Null }

Write-Host ""
Write-Host ".env created." -ForegroundColor Green
Write-Host ""
Write-Host "Management panel sign-in (you will be asked to set a new password on first sign-in):"
Write-Host "  user name        : admin   (e-mail: $adminEmail)"
Write-Host "  initial password : $adminPass"
Write-Host ""
Write-Host "Next step:"
Write-Host "  docker compose up -d"
Write-Host "  Panel: $($values.PANEL_PUBLIC_URL)    Booster Auth: $publicUrl/idp"
Write-Host ""
Write-Host "On first start the panel opens in 'no license' mode (read-only). Use the 'Get a license' button on"
Write-Host "System > License to go to the licensing portal, get a trial or paid license and paste it."

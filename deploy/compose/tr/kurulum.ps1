# Booster Auth kurulum betigi (Windows PowerShell 5.1+ / PowerShell 7).
# Adresleri sorar, parola ve anahtarlari rastgele uretir, .env dosyasini yazar.
# Kullanim:  powershell -ExecutionPolicy Bypass -File .\kurulum.ps1
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

if (Test-Path .env) {
    Write-Host ".env zaten var; uzerine yazilmadi. Yeniden kurulum icin once .env'yi yedekleyip silin." -ForegroundColor Yellow
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
    return "$p-A7k!"   # buyuk/kucuk harf, rakam ve sembol garanti
}
function Ask([string]$question, [string]$default) {
    $a = Read-Host "$question [$default]"
    if ([string]::IsNullOrWhiteSpace($a)) { return $default } else { return $a.Trim() }
}

Write-Host ""
Write-Host "Booster Auth kurulumu" -ForegroundColor Cyan
Write-Host "Booster Auth'un adresi lisansinizdaki domain'lerden biri olmalidir (or. auth.firma.com)."
Write-Host "Yerel deneme icin hosts dosyasina '127.0.0.1 <adres>' satiri ekleyebilirsiniz."
Write-Host ""

$publicHost = Ask "Booster Auth adresi (host)" "auth.firma.com"
$publicHost = ($publicHost -replace '^https?://', '').TrimEnd('/')
$httpsPort  = Ask "HTTPS portu" "443"
$publicUrl  = if ($httpsPort -eq '443') { "https://$publicHost" } else { "https://${publicHost}:$httpsPort" }
$panelPort  = Ask "Yonetim paneli portu" "3000"
$panelUrl   = Ask "Yonetim paneli adresi" "http://localhost:$panelPort"
$adminEmail = Ask "Yonetici e-postasi" "admin@booster.local"
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
}

$lines = Get-Content .env.example
$out = foreach ($line in $lines) {
    if ($line -match '^([A-Z0-9_]+)=') {
        $key = $Matches[1]
        if ($values.Contains($key)) { "$key=$($values[$key])"; continue }
    }
    $line
}
# UTF-8 (BOM'suz) ve LF: docker compose her ikisini de okur; BOM'suz olmasi Linux'a tasimayi kolaylastirir.
[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot '.env'), (($out -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding $false))

foreach ($d in 'data', 'pki', 'secrets') { New-Item -ItemType Directory -Force -Path $d | Out-Null }

Write-Host ""
Write-Host ".env olusturuldu." -ForegroundColor Green
Write-Host ""
Write-Host "Yonetim paneli girisi (ilk giriste yeni parola belirlemeniz istenir):"
Write-Host "  kullanici adi : admin   (e-posta: $adminEmail)"
Write-Host "  ilk parola    : $adminPass"
Write-Host ""
Write-Host "Log ve izleme arayuzu (OpenObserve, yerel ag): http://<sunucu>:5080"
Write-Host "  e-posta : admin@booster.local"
Write-Host "  parola  : $o2Pass   (.env: OPENOBSERVE_ADMIN_PASSWORD)"
Write-Host ""
Write-Host "Sonraki adim:"
Write-Host "  docker compose up -d"
Write-Host "  Panel: $($values.PANEL_PUBLIC_URL)    Booster Auth: $publicUrl/idp"
Write-Host ""
Write-Host "Ilk aciliste panel 'lisans yok' modunda (salt okunur) acilir. Sistem > Lisans ekranindaki"
Write-Host "'Lisans al' butonuyla lisans portalina gidin, deneme ya da ucretli lisans alip yapistirin."

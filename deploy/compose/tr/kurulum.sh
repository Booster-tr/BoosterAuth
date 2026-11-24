#!/usr/bin/env bash
# Booster Auth kurulum betigi (Linux / macOS; bash 3.2+).
# Adresleri sorar, parola ve anahtarlari rastgele uretir, .env dosyasini yazar.
# Kullanim:  ./kurulum.sh
set -euo pipefail
cd "$(dirname "$0")"

if [ -e .env ]; then
    echo ".env zaten var; uzerine yazilmadi. Yeniden kurulum icin once .env'yi yedekleyip silin." >&2
    exit 1
fi

hex() { head -c "$1" /dev/urandom | od -An -tx1 | tr -d ' \n'; }
b64() { head -c "$1" /dev/urandom | base64 | tr -d '\n'; }
admin_password() {
    local p
    p=$(LC_ALL=C tr -dc 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789' < /dev/urandom | head -c 16 || true)
    echo "${p}-A7k!"   # buyuk/kucuk harf, rakam ve sembol garanti
}
ask() { local a; read -r -p "$1 [$2]: " a || true; echo "${a:-$2}"; }

echo
echo "Booster Auth kurulumu"
echo "Booster Auth'un adresi lisansinizdaki domain'lerden biri olmalidir (or. auth.firma.com)."
echo "Yerel deneme icin /etc/hosts dosyasina '127.0.0.1 <adres>' satiri ekleyebilirsiniz."
echo

PUBLIC_HOST=$(ask "Booster Auth adresi (host)" "auth.firma.com")
PUBLIC_HOST=${PUBLIC_HOST#http://}; PUBLIC_HOST=${PUBLIC_HOST#https://}; PUBLIC_HOST=${PUBLIC_HOST%/}
GATEWAY_HTTPS_PORT=$(ask "HTTPS portu" "443")
if [ "$GATEWAY_HTTPS_PORT" = "443" ]; then PUBLIC_URL="https://$PUBLIC_HOST"; else PUBLIC_URL="https://$PUBLIC_HOST:$GATEWAY_HTTPS_PORT"; fi
PANEL_PORT=$(ask "Yonetim paneli portu" "3000")
PANEL_PUBLIC_URL=$(ask "Yonetim paneli adresi" "http://localhost:$PANEL_PORT"); PANEL_PUBLIC_URL=${PANEL_PUBLIC_URL%/}
ADMIN_EMAIL=$(ask "Yonetici e-postasi" "admin@booster.local")
BOOTSTRAP_ADMIN_EMAIL=$ADMIN_EMAIL
if [ "$ADMIN_EMAIL" = "admin@booster.local" ]; then BOOTSTRAP_ADMIN_EMAIL=""; fi

BOOTSTRAP_ADMIN_PASSWORD=$(admin_password)
POSTGRES_PASSWORD=$(hex 24); REDIS_PASSWORD=$(hex 24); OPENFGA_PRESHARED_KEY=$(hex 24)
HASHING_PEPPER=$(hex 32); HASHING_ENCRYPTION_KEY=$(b64 32); IDP_APPLICATION_PEPPER=$(hex 32); PANEL_SESSION_SECRET=$(hex 32)
# OpenObserve guclu parola ister (buyuk + kucuk harf, rakam, ozel karakter): sabit onek + rastgele.
OPENOBSERVE_ADMIN_PASSWORD="Bo-9$(hex 16)"

# .env.example'daki bu anahtarlar yukaridaki degiskenlerle doldurulur (dolayli erisim: ${!key}).
KEYS=" PUBLIC_HOST GATEWAY_HTTPS_PORT PUBLIC_URL PANEL_PORT PANEL_PUBLIC_URL BOOTSTRAP_ADMIN_EMAIL BOOTSTRAP_ADMIN_PASSWORD POSTGRES_PASSWORD REDIS_PASSWORD OPENFGA_PRESHARED_KEY HASHING_PEPPER HASHING_ENCRYPTION_KEY IDP_APPLICATION_PEPPER PANEL_SESSION_SECRET OPENOBSERVE_ADMIN_PASSWORD "

umask 077
tr -d '\r' < .env.example | while IFS= read -r line; do
    key=${line%%=*}
    case "$line" in
        [A-Z]*=*)
            case "$KEYS" in
                *" $key "*) printf '%s=%s\n' "$key" "${!key}"; continue ;;
            esac ;;
    esac
    printf '%s\n' "$line"
done > .env
mkdir -p data pki secrets

echo
echo ".env olusturuldu."
echo
echo "Yonetim paneli girisi (ilk giriste yeni parola belirlemeniz istenir):"
echo "  kullanici adi : admin   (e-posta: $ADMIN_EMAIL)"
echo "  ilk parola    : $BOOTSTRAP_ADMIN_PASSWORD"
echo
echo "Log ve izleme arayuzu (OpenObserve, yerel ag): http://<sunucu>:5080"
echo "  e-posta : admin@booster.local"
echo "  parola  : $OPENOBSERVE_ADMIN_PASSWORD   (.env: OPENOBSERVE_ADMIN_PASSWORD)"
echo
echo "Sonraki adim:"
echo "  docker compose up -d"
echo "  Panel: $PANEL_PUBLIC_URL    Booster Auth: $PUBLIC_URL/idp"
echo
echo "Ilk aciliste panel 'lisans yok' modunda (salt okunur) acilir. Sistem > Lisans ekranindaki"
echo "'Lisans al' butonuyla lisans portalina gidin, deneme ya da ucretli lisans alip yapistirin."

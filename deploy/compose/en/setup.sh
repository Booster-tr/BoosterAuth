#!/usr/bin/env bash
# Booster Auth setup script (Linux / macOS; bash 3.2+).
# Asks for the addresses, generates random passwords and keys, and writes the .env file.
# Usage:  ./setup.sh
set -euo pipefail
cd "$(dirname "$0")"

if [ -e .env ]; then
    echo ".env already exists and was not overwritten. To reinstall, back up and delete .env first." >&2
    exit 1
fi

hex() { head -c "$1" /dev/urandom | od -An -tx1 | tr -d ' \n'; }
b64() { head -c "$1" /dev/urandom | base64 | tr -d '\n'; }
admin_password() {
    local p
    p=$(LC_ALL=C tr -dc 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789' < /dev/urandom | head -c 16 || true)
    echo "${p}-A7k!"   # guarantees upper/lower case, digit and symbol
}
ask() { local a; read -r -p "$1 [$2]: " a || true; echo "${a:-$2}"; }

echo
echo "Booster Auth setup"
echo "The Booster Auth address must be one of the domains in your license (e.g. auth.company.com)."
echo "For a local trial you can add a '127.0.0.1 <address>' line to /etc/hosts."
echo

PUBLIC_HOST=$(ask "Booster Auth address (host)" "auth.company.com")
PUBLIC_HOST=${PUBLIC_HOST#http://}; PUBLIC_HOST=${PUBLIC_HOST#https://}; PUBLIC_HOST=${PUBLIC_HOST%/}
GATEWAY_HTTPS_PORT=$(ask "HTTPS port" "443")
if [ "$GATEWAY_HTTPS_PORT" = "443" ]; then PUBLIC_URL="https://$PUBLIC_HOST"; else PUBLIC_URL="https://$PUBLIC_HOST:$GATEWAY_HTTPS_PORT"; fi
PANEL_PORT=$(ask "Management panel port" "3000")
PANEL_PUBLIC_URL=$(ask "Management panel address" "http://localhost:$PANEL_PORT"); PANEL_PUBLIC_URL=${PANEL_PUBLIC_URL%/}
ADMIN_EMAIL=$(ask "Administrator e-mail" "admin@booster.local")
BOOTSTRAP_ADMIN_EMAIL=$ADMIN_EMAIL
if [ "$ADMIN_EMAIL" = "admin@booster.local" ]; then BOOTSTRAP_ADMIN_EMAIL=""; fi

BOOTSTRAP_ADMIN_PASSWORD=$(admin_password)
POSTGRES_PASSWORD=$(hex 24); REDIS_PASSWORD=$(hex 24); OPENFGA_PRESHARED_KEY=$(hex 24)
HASHING_PEPPER=$(hex 32); HASHING_ENCRYPTION_KEY=$(b64 32); IDP_APPLICATION_PEPPER=$(hex 32); PANEL_SESSION_SECRET=$(hex 32)
# OpenObserve guclu parola ister (buyuk + kucuk harf, rakam, ozel karakter): sabit onek + rastgele.
OPENOBSERVE_ADMIN_PASSWORD="Bo-9$(hex 16)"

# These keys in .env.example are filled from the variables above (indirect expansion: ${!key}).
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
echo ".env created."
echo
echo "Management panel sign-in (you will be asked to set a new password on first sign-in):"
echo "  user name        : admin   (e-mail: $ADMIN_EMAIL)"
echo "  initial password : $BOOTSTRAP_ADMIN_PASSWORD"
echo
echo "Logs and monitoring UI (OpenObserve, local network): http://<server>:5080"
echo "  e-mail   : admin@booster.local"
echo "  password : $OPENOBSERVE_ADMIN_PASSWORD   (.env: OPENOBSERVE_ADMIN_PASSWORD)"
echo
echo "Next step:"
echo "  docker compose up -d"
echo "  Panel: $PANEL_PUBLIC_URL    Booster Auth: $PUBLIC_URL/idp"
echo
echo "On first start the panel opens in 'no license' mode (read-only). Use the 'Get a license' button on"
echo "System > License to go to the licensing portal, get a trial or paid license and paste it."

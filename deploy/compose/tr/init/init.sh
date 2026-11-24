#!/usr/bin/env bash
# booster-init: idempotent hazirlik. Var olan anahtarlara dokunmaz; yalnizca
# eksik olani uretir veya .env'deki PUBLIC_HOST degistiyse sunucu sertifikasini
# yeniler.
#
#   /work/pki/ca     -> ca.key            (YALNIZCA bu container)
#   /work/pki/trust  -> ca.crt            (tum servisler)
#   /work/pki/tls    -> server.crt/.key   (yalnizca gateway)
#   /work/secrets  -> idp-signing.pem
#   /work/data/idp -> IdP'nin yazdigi klasorler (UID 1654'e verilir)
set -euo pipefail

: "${PUBLIC_HOST:?PUBLIC_HOST tanimli degil}"

# ------------------------------------------------------------------ .env kontrolu
# .env.example'daki "CHANGE_ME" yer tutucusu birakildiysa zayif/bilinen bir gizli degerle calismak yerine dur.
bad=""
for v in POSTGRES_PASSWORD REDIS_PASSWORD OPENFGA_PRESHARED_KEY HASHING_PEPPER HASHING_ENCRYPTION_KEY IDP_APPLICATION_PEPPER PANEL_SESSION_SECRET BOOTSTRAP_ADMIN_PASSWORD; do
    val="$(printenv "CHECK_$v" || true)"
    if [ -z "$val" ] || [ "$val" = "CHANGE_ME" ]; then bad="$bad $v"; fi
done
if [ -n "$bad" ]; then
    echo "[init] HATA: .env icinde su degerler bos ya da CHANGE_ME:$bad" >&2
    echo "[init] Kurulum betigini calistirin (./kurulum.sh ya da kurulum.ps1) veya bu degerleri uzun rastgele degerlerle doldurun." >&2
    exit 1
fi
APP_UID="${APP_UID:-1654}"
CA_DIR=/work/pki/ca
TRUST_DIR=/work/pki/trust
TLS_DIR=/work/pki/tls
SECRETS=/work/secrets
IDP_DATA=/work/data/idp

mkdir -p "$CA_DIR" "$TRUST_DIR" "$TLS_DIR" "$SECRETS" "$IDP_DATA/dataprotection" "$IDP_DATA/client-logos"

# ------------------------------------------------------------------ lokal CA
if [ ! -s "$TRUST_DIR/ca.crt" ] || [ ! -s "$CA_DIR/ca.key" ]; then
    echo "[init] Lokal CA olusturuluyor"
    openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
        -keyout "$CA_DIR/ca.key" -out "$TRUST_DIR/ca.crt" \
        -subj "/CN=Booster Auth Local CA" \
        -addext "basicConstraints=critical,CA:TRUE" \
        -addext "keyUsage=critical,keyCertSign,cRLSign" 2>/dev/null
    # Yalnizca BIZIM sertifikamiz yeni CA ile yeniden imzalanir; sizin sertifikaniza dokunulmaz.
    if openssl x509 -in "$TLS_DIR/server.crt" -noout -issuer 2>/dev/null | grep -q "Booster Auth Local CA"; then
        rm -f "$TLS_DIR/server.crt"
    fi
fi

# ------------------------------------------------------------------ gateway TLS
# Iki tur sertifika olabilir:
#  - Bizim urettigimiz (yerel CA imzali; issuer "Booster Auth Local CA"): eksikse, adresi (PUBLIC_HOST) tutmuyorsa,
#    30 gun icinde doluyorsa ya da CA degistiyse yeniden uretilir.
#  - Sizin koydugunuz (baska bir CA, or. kurum ya da Let's Encrypt): ASLA degistirilmez; yalnizca uyari yazilir.
#    Yenilemesi sizdedir. Bu kural olmadan init her acilista sizin sertifikanizi yerel CA sertifikasiyla eziyordu.
is_local_cert() {
    openssl x509 -in "$TLS_DIR/server.crt" -noout -issuer 2>/dev/null | grep -q "Booster Auth Local CA"
}
has_public_host() {
    openssl x509 -in "$TLS_DIR/server.crt" -noout -ext subjectAltName 2>/dev/null | grep -q "DNS:${PUBLIC_HOST}\(,\|$\)"
}
expires_soon() {
    ! openssl x509 -in "$TLS_DIR/server.crt" -noout -checkend $((30*24*3600)) >/dev/null 2>&1
}

need_cert=false
if [ ! -s "$TLS_DIR/server.crt" ] || [ ! -s "$TLS_DIR/server.key" ]; then
    need_cert=true
elif is_local_cert; then
    if ! has_public_host; then
        echo "[init] PUBLIC_HOST (${PUBLIC_HOST}) sertifikada yok"
        need_cert=true
    elif expires_soon; then
        echo "[init] Sertifikanin suresi 30 gun icinde doluyor"
        need_cert=true
    elif ! openssl verify -CAfile "$TRUST_DIR/ca.crt" "$TLS_DIR/server.crt" >/dev/null 2>&1; then
        echo "[init] Sertifika mevcut CA ile imzali degil"
        need_cert=true
    fi
else
    echo "[init] Kendi sertifikaniz kullaniliyor (pki/tls); dokunulmuyor."
    has_public_host || echo "[init] UYARI: sertifikanizda PUBLIC_HOST (${PUBLIC_HOST}) yok; tarayici uyari verebilir."
    expires_soon && echo "[init] UYARI: sertifikanizin suresi 30 gun icinde doluyor; yenileyip pki/tls'e koyun."
fi

if [ "$need_cert" = true ]; then
    echo "[init] Gateway sertifikasi uretiliyor (SAN: ${PUBLIC_HOST}, localhost)"
    tmp="$(mktemp -d)"
    cat > "$tmp/server.ext" <<EOF
basicConstraints=CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:${PUBLIC_HOST},DNS:localhost,DNS:booster-gateway,IP:127.0.0.1
EOF
    openssl req -newkey rsa:2048 -nodes -sha256 \
        -keyout "$TLS_DIR/server.key" -out "$tmp/server.csr" \
        -subj "/CN=${PUBLIC_HOST}" 2>/dev/null
    openssl x509 -req -sha256 -days 825 \
        -in "$tmp/server.csr" -CA "$TRUST_DIR/ca.crt" -CAkey "$CA_DIR/ca.key" -CAcreateserial \
        -CAserial "$tmp/ca.srl" -extfile "$tmp/server.ext" -out "$TLS_DIR/server.crt" 2>/dev/null
    rm -rf "$tmp"
fi

# ------------------------------------------------------------------ IdP imzalama anahtari
if [ ! -s "$SECRETS/idp-signing.pem" ]; then
    echo "[init] IdP OIDC imzalama anahtari uretiliyor"
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$SECRETS/idp-signing.pem" 2>/dev/null
fi

# ------------------------------------------------------------------ izinler
# Servisler root olmayan kullanicilarla calisir. CA ozel anahtari hicbir
# servise baglanmaz; yalnizca bu container gorur.
chmod 0644 "$TRUST_DIR/ca.crt" "$TLS_DIR/server.crt" "$TLS_DIR/server.key" "$SECRETS/idp-signing.pem"
chmod 0600 "$CA_DIR/ca.key"
chown -R "${APP_UID}:${APP_UID}" "$IDP_DATA"

echo "[init] Hazir."

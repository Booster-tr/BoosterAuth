#!/usr/bin/env bash
# booster-init: idempotent preparation. Never touches existing keys; only
# creates what is missing, or renews the server certificate if PUBLIC_HOST in
# .env has changed.
#
#   /work/pki/ca     -> ca.key            (ONLY this container)
#   /work/pki/trust  -> ca.crt            (all services)
#   /work/pki/tls    -> server.crt/.key   (gateway only)
#   /work/secrets  -> idp-signing.pem
#   /work/data/idp -> folders written by the IdP (owned by UID 1654)
set -euo pipefail

: "${PUBLIC_HOST:?PUBLIC_HOST is not set}"

# ------------------------------------------------------------------ .env check
# If the "CHANGE_ME" placeholder from .env.example was left, stop instead of running with a weak/known secret.
bad=""
for v in POSTGRES_PASSWORD REDIS_PASSWORD OPENFGA_PRESHARED_KEY HASHING_PEPPER HASHING_ENCRYPTION_KEY IDP_APPLICATION_PEPPER PANEL_SESSION_SECRET BOOTSTRAP_ADMIN_PASSWORD; do
    val="$(printenv "CHECK_$v" || true)"
    if [ -z "$val" ] || [ "$val" = "CHANGE_ME" ]; then bad="$bad $v"; fi
done
if [ -n "$bad" ]; then
    echo "[init] ERROR: these values in .env are empty or CHANGE_ME:$bad" >&2
    echo "[init] Run the setup script (./setup.sh or setup.ps1) or fill these values with long random values." >&2
    exit 1
fi
APP_UID="${APP_UID:-1654}"
CA_DIR=/work/pki/ca
TRUST_DIR=/work/pki/trust
TLS_DIR=/work/pki/tls
SECRETS=/work/secrets
IDP_DATA=/work/data/idp

mkdir -p "$CA_DIR" "$TRUST_DIR" "$TLS_DIR" "$SECRETS" "$IDP_DATA/dataprotection" "$IDP_DATA/client-logos"

# ------------------------------------------------------------------ local CA
if [ ! -s "$TRUST_DIR/ca.crt" ] || [ ! -s "$CA_DIR/ca.key" ]; then
    echo "[init] Creating local CA"
    openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
        -keyout "$CA_DIR/ca.key" -out "$TRUST_DIR/ca.crt" \
        -subj "/CN=Booster Auth Local CA" \
        -addext "basicConstraints=critical,CA:TRUE" \
        -addext "keyUsage=critical,keyCertSign,cRLSign" 2>/dev/null
    # Only OUR certificate is re-signed with the new CA; yours is left alone.
    if openssl x509 -in "$TLS_DIR/server.crt" -noout -issuer 2>/dev/null | grep -q "Booster Auth Local CA"; then
        rm -f "$TLS_DIR/server.crt"
    fi
fi

# ------------------------------------------------------------------ gateway TLS
# There are two kinds of certificate:
#  - Ours (signed by the local CA; issuer "Booster Auth Local CA"): regenerated when missing, when it does not match
#    PUBLIC_HOST, when it expires within 30 days or when the CA changed.
#  - Yours (another CA, e.g. corporate or Let's Encrypt): NEVER changed; only a warning is written. Renewing it is up
#    to you. Without this rule init replaced your certificate with a local-CA one on every start.
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
        echo "[init] PUBLIC_HOST (${PUBLIC_HOST}) is not in the certificate"
        need_cert=true
    elif expires_soon; then
        echo "[init] Certificate expires within 30 days"
        need_cert=true
    elif ! openssl verify -CAfile "$TRUST_DIR/ca.crt" "$TLS_DIR/server.crt" >/dev/null 2>&1; then
        echo "[init] Certificate is not signed by the current CA"
        need_cert=true
    fi
else
    echo "[init] Using your own certificate (pki/tls); leaving it unchanged."
    has_public_host || echo "[init] WARNING: PUBLIC_HOST (${PUBLIC_HOST}) is not in your certificate; browsers may warn."
    expires_soon && echo "[init] WARNING: your certificate expires within 30 days; renew it and put it in pki/tls."
fi

if [ "$need_cert" = true ]; then
    echo "[init] Generating gateway certificate (SAN: ${PUBLIC_HOST}, localhost)"
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

# ------------------------------------------------------------------ IdP signing key
if [ ! -s "$SECRETS/idp-signing.pem" ]; then
    echo "[init] Generating IdP OIDC signing key"
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$SECRETS/idp-signing.pem" 2>/dev/null
fi

# ------------------------------------------------------------------ permissions
# Services run as non-root users. The CA private key is never mounted into
# a service; only this container sees it.
chmod 0644 "$TRUST_DIR/ca.crt" "$TLS_DIR/server.crt" "$TLS_DIR/server.key" "$SECRETS/idp-signing.pem"
chmod 0600 "$CA_DIR/ca.key"
chown -R "${APP_UID}:${APP_UID}" "$IDP_DATA"

echo "[init] Ready."

#!/bin/sh
set -eu

# EXTRA_CA_CERTS: container'ın güvenmesi gereken ek CA sertifikası (PEM) yolu.
# Lokal/kurum içi CA ile imzalanmış issuer'a (gateway) HTTPS ile gidebilmek için
# kullanılır. Root olmadan sistem deposunu değiştiremeyeceğimiz için sistem
# CA'ları + ek CA birleştirilip SSL_CERT_FILE ile .NET/OpenSSL'e gösterilir.
if [ -n "${EXTRA_CA_CERTS:-}" ]; then
    if [ ! -r "${EXTRA_CA_CERTS}" ]; then
        echo "EXTRA_CA_CERTS okunamıyor: ${EXTRA_CA_CERTS}" >&2
        exit 1
    fi
    bundle="${TMPDIR:-/tmp}/ca-bundle.crt"
    cat /etc/ssl/certs/ca-certificates.crt "${EXTRA_CA_CERTS}" > "${bundle}"
    export SSL_CERT_FILE="${bundle}"
fi

# Not: /bin/sh, adi gecerli shell tanimlayicisi olmayan (ör. "-" iceren)
# env degiskenlerini alt surece aktarmaz; config anahtarlarinda "-" kullanmayin.
exec dotnet "/app/${APP_DLL}" "$@"

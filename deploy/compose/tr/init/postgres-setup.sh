#!/bin/bash
# postgres-setup: her "up"ta calisir; .env'deki ayarlari mevcut veritabanina uygular.
#   - POSTGRES_PASSWORD degistiyse rol parolasini gunceller
#     (postgres imaji POSTGRES_PASSWORD'u yalnizca ILK kurulumda kullanir)
#   - POSTGRES_DATABASES icindeki eksik veritabanlarini olusturur
# Postgres'e unix socket uzerinden (local = trust) baglanir; parola gerekmez.
set -euo pipefail

PSQL=(psql -v ON_ERROR_STOP=1 -h /var/run/postgresql -U "$POSTGRES_USER" -d postgres -qtA)

for i in $(seq 1 30); do
    "${PSQL[@]}" -c 'SELECT 1' >/dev/null 2>&1 && break
    echo "[postgres-setup] Postgres bekleniyor ($i)"; sleep 2
done

echo "[postgres-setup] '$POSTGRES_USER' parolasi .env ile esitleniyor"
"${PSQL[@]}" -v user="$POSTGRES_USER" -v pass="$POSTGRES_PASSWORD" <<'EOSQL'
ALTER ROLE :"user" WITH PASSWORD :'pass';
EOSQL

IFS=',' read -ra databases <<< "${POSTGRES_DATABASES:-}"
for db in "${databases[@]}"; do
    db="$(echo "$db" | xargs)"
    [ -z "$db" ] && continue
    exists="$("${PSQL[@]}" -v db="$db" <<'EOSQL'
SELECT 1 FROM pg_database WHERE datname = :'db';
EOSQL
)"
    if [ "$exists" != "1" ]; then
        echo "[postgres-setup] Veritabani olusturuluyor: $db"
        "${PSQL[@]}" -v db="$db" <<'EOSQL'
CREATE DATABASE :"db";
EOSQL
    fi
done

echo "[postgres-setup] Hazir."

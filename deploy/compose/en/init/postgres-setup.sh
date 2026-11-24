#!/bin/bash
# postgres-setup: runs on every "up"; applies the .env settings to the existing database.
#   - updates the role password if POSTGRES_PASSWORD changed
#     (the postgres image uses POSTGRES_PASSWORD only on FIRST setup)
#   - creates missing databases listed in POSTGRES_DATABASES
# Connects to Postgres over the unix socket (local = trust); no password needed.
set -euo pipefail

PSQL=(psql -v ON_ERROR_STOP=1 -h /var/run/postgresql -U "$POSTGRES_USER" -d postgres -qtA)

for i in $(seq 1 30); do
    "${PSQL[@]}" -c 'SELECT 1' >/dev/null 2>&1 && break
    echo "[postgres-setup] Waiting for Postgres ($i)"; sleep 2
done

echo "[postgres-setup] '$POSTGRES_USER' password is being synced with .env"
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
        echo "[postgres-setup] Creating database: $db"
        "${PSQL[@]}" -v db="$db" <<'EOSQL'
CREATE DATABASE :"db";
EOSQL
    fi
done

echo "[postgres-setup] Ready."

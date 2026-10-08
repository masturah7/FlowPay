#!/bin/bash
# Creates one database per service (database-per-service) on first container
# start, since the official postgres image only creates the single database
# named by POSTGRES_DB. Driven by the POSTGRES_MULTIPLE_DATABASES env var
# (comma-separated list) set in docker-compose.yml.
set -euo pipefail

if [ -z "${POSTGRES_MULTIPLE_DATABASES:-}" ]; then
  exit 0
fi

echo "Creating databases: ${POSTGRES_MULTIPLE_DATABASES}"

IFS=',' read -ra DATABASES <<< "${POSTGRES_MULTIPLE_DATABASES}"
for db in "${DATABASES[@]}"; do
  echo "  -> $db"
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    SELECT 'CREATE DATABASE "$db"'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db')\gexec
EOSQL
done

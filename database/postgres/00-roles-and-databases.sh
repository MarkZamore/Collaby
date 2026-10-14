#!/bin/sh
set -eu

psql_admin() {
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" "$@"
}

ensure_role_and_database() {
  role_name="$1"
  role_password="$2"
  database_name="$3"

  if [ -z "$role_name" ] || [ -z "$role_password" ] || [ -z "$database_name" ]; then
    echo "Service-scoped Postgres credentials are not fully configured for database '$database_name'." >&2
    exit 1
  fi

  psql_admin \
    -v role_name="$role_name" \
    -v role_password="$role_password" \
    -v database_name="$database_name" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'role_name', :'role_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role_name') \gexec

SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'role_name', :'role_password')
WHERE EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role_name') \gexec

SELECT format('CREATE DATABASE %I OWNER %I', :'database_name', :'role_name')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'database_name') \gexec

REVOKE ALL ON DATABASE :"database_name" FROM PUBLIC;
GRANT ALL PRIVILEGES ON DATABASE :"database_name" TO :"role_name";
ALTER DATABASE :"database_name" OWNER TO :"role_name";
SQL

  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$database_name" -v role_name="$role_name" <<'SQL'
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO :"role_name";
ALTER SCHEMA public OWNER TO :"role_name";
SQL
}

has_legacy_service_credentials=0

for value in \
  "${POSTGRES_IDENTITY_USER:-}" \
  "${POSTGRES_USERS_USER:-}" \
  "${POSTGRES_MANAGEMENT_USER:-}" \
  "${POSTGRES_GAMIFICATION_USER:-}" \
  "${POSTGRES_ANALYTICS_USER:-}"
do
  if [ -n "$value" ]; then
    has_legacy_service_credentials=1
    break
  fi
done

if [ "$has_legacy_service_credentials" -eq 0 ]; then
  echo "No legacy shared Postgres bootstrap variables detected. Skipping shared role/database bootstrap."
  exit 0
fi

ensure_role_and_database "$POSTGRES_IDENTITY_USER" "$POSTGRES_IDENTITY_PASSWORD" "collaby_identity"
ensure_role_and_database "$POSTGRES_USERS_USER" "$POSTGRES_USERS_PASSWORD" "collaby_users"
ensure_role_and_database "$POSTGRES_MANAGEMENT_USER" "$POSTGRES_MANAGEMENT_PASSWORD" "collaby_management"
ensure_role_and_database "$POSTGRES_GAMIFICATION_USER" "$POSTGRES_GAMIFICATION_PASSWORD" "collaby_gamification"
ensure_role_and_database "$POSTGRES_ANALYTICS_USER" "$POSTGRES_ANALYTICS_PASSWORD" "collaby_analytics"

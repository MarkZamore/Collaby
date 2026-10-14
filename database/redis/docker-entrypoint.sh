#!/bin/sh
set -eu

ACL_FILE=/usr/local/etc/redis/users.acl
REDIS_CONFIG=/usr/local/etc/redis/redis.conf

require_var() {
  var_name="$1"
  eval "var_value=\${$var_name:-}"
  if [ -z "$var_value" ]; then
    echo "Missing required Redis ACL variable: $var_name" >&2
    exit 1
  fi
}

write_service_user() {
  username="$1"
  password="$2"
  key_pattern="$3"

  printf 'user %s on >%s ~%s &* +@all -acl -config -shutdown -flushall -flushdb -save -bgsave -bgrewriteaof -module -migrate -monitor -swapdb\n' \
    "$username" "$password" "$key_pattern" >> "$ACL_FILE"
}

# Service-owned Redis containers должны стартовать по сокращённому ACL-контракту
# через REDIS_APP_*; legacy full-ACL ветка ниже нужна только для shared-runtime cutover.
if [ -n "${REDIS_APP_USER:-}" ] || [ -n "${REDIS_APP_PASSWORD:-}" ]; then
  require_var REDIS_APP_USER
  require_var REDIS_APP_PASSWORD

  app_key_pattern="${REDIS_APP_KEY_PATTERN:-*}"

  cat > "$ACL_FILE" <<EOF
user default off
user ${REDIS_APP_USER} on >${REDIS_APP_PASSWORD} ~${app_key_pattern} &* +@all -acl -config -shutdown -flushall -flushdb -save -bgsave -bgrewriteaof -module -migrate -monitor -swapdb
EOF

  chmod 600 "$ACL_FILE"
  exec redis-server "$REDIS_CONFIG"
fi

require_var REDIS_ADMIN_USER
require_var REDIS_ADMIN_PASSWORD
require_var REDIS_IDENTITY_USER
require_var REDIS_IDENTITY_PASSWORD
require_var REDIS_MANAGEMENT_USER
require_var REDIS_MANAGEMENT_PASSWORD
require_var REDIS_MESSENGER_USER
require_var REDIS_MESSENGER_PASSWORD
require_var REDIS_CALLS_USER
require_var REDIS_CALLS_PASSWORD
require_var REDIS_GAMIFICATION_USER
require_var REDIS_GAMIFICATION_PASSWORD

# default-пользователь выключен, чтобы bounded contexts не делили общий credential.
cat > "$ACL_FILE" <<EOF
user default off
user ${REDIS_ADMIN_USER} on >${REDIS_ADMIN_PASSWORD} ~* &* +@all
EOF

write_service_user "$REDIS_IDENTITY_USER" "$REDIS_IDENTITY_PASSWORD" "identity:*"
write_service_user "$REDIS_MANAGEMENT_USER" "$REDIS_MANAGEMENT_PASSWORD" "management:*"
write_service_user "$REDIS_MESSENGER_USER" "$REDIS_MESSENGER_PASSWORD" "messenger:*"
write_service_user "$REDIS_CALLS_USER" "$REDIS_CALLS_PASSWORD" "calls:*"
write_service_user "$REDIS_GAMIFICATION_USER" "$REDIS_GAMIFICATION_PASSWORD" "gamification:*"

chmod 600 "$ACL_FILE"

exec redis-server "$REDIS_CONFIG"

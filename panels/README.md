# panels

## Назначение

Локальные панели Kubernetes. Grafana, pgAdmin, Mongo Express, Kafka UI и Portainer используют обычный вход. RedisInsight, Mailpit и Netdata доступны через локальный Caddy basic_auth.

## Что здесь является источником истины

`frontend/Caddyfile`, `kubernetes/runtime-spec.json`, `scripts/render-local.py`.

## Что агент должен знать перед изменениями

Runtime определён в корневом README. Пароли сохраняются в исключённом `.local/rendered/local.json`. Cloudflare выключен по умолчанию.

## Локальные команды/проверки

`start-local.cmd`; `cd frontend; npm test; npm run build`.

## Связанные документы

[Корневой README](../README.md).

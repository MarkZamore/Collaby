# panels

## Назначение

Портал Swagger по адресу http://localhost:8080/swagger/. Caddy проксирует Swagger каждого укрупнённого host.

## Что здесь является источником истины

`frontend/Caddyfile`, `kubernetes/runtime-spec.json`, `scripts/render-local.py`.

## Что агент должен знать перед изменениями

Runtime определён в корневом README. Пароли сохраняются в исключённом `.local/rendered/local.json`. Cloudflare выключен по умолчанию.

## Локальные команды/проверки

`start-local.cmd`; `cd frontend; npm test; npm run build`.

## Связанные документы

[Корневой README](../../README.md).

# panels

## Назначение

Локальные панели Kubernetes. Grafana, pgAdmin, Mongo Express, Kafka UI и Portainer используют обычный вход. RedisInsight, Mailpit и Netdata доступны через локальный Caddy basic_auth.

## Что здесь является источником истины

`kubernetes/runtime-spec.json`, `scripts/render-local.py`, `panels/grafana/`.

## Что агент должен знать перед изменениями

Runtime определён в корневом README. Пароли сохраняются в исключённом `.local/rendered/local.json`. Cloudflare выключен по умолчанию.

Откройте `.local/rendered/local.json` на этой машине и используйте значение `PANEL_PASSWORD` для входа в панели. Не копируйте файл в репозиторий или журналы.

| Панель | Логин | Источник логина |
| --- | --- | --- |
| Grafana | При первичной генерации `grafana_admin_user` | `GRAFANA_ADMIN_USER` в `local.json` |
| pgAdmin | `admin@collaby.org` | `PGADMIN_DEFAULT_EMAIL` в `local.json` |
| Mongo Express | `admin` | `ME_CONFIG_BASICAUTH_USERNAME` в runtime spec |
| Kafka UI | `admin` | `SPRING_SECURITY_USER_NAME` в runtime spec |
| Portainer | `admin` | `scripts/seed-local.py` |
| RedisInsight, Netdata, Mailpit | `admin` в диалоге браузера | Caddy gateway в renderer |

pgAdmin автоматически подключает пять логических БД через защищённый `.pgpass`; повторно вводить пароли PostgreSQL не требуется. RedisInsight подключается к `cache` с `REDIS_ADMIN_USER` и `REDIS_ADMIN_PASSWORD`, которые отличаются от браузерного входа в панель. Kafka UI показывает кластер `Collaby Kafka`; его форма входа использует `/auth`. Grafana получает данные из Loki, Prometheus и Tempo через внутренние Kubernetes Services.

## Локальные команды/проверки

`start-local.cmd`; `python scripts/render-local.py --validate`; `python -m unittest discover -s scripts/tests`.

## Связанные документы

[Корневой README](../README.md).

# Event Contracts

## Назначение

`platform/contracts/events/` хранит repo-owned Kafka topic registry и payload-only JSON Schema для межсервисных событий `Collaby`.

## Что здесь является источником истины

- `topic-registry.json` — source of truth для canonical topic names, версий, DLQ и обязательных headers.
- `*.schema.json` — source of truth для payload-контрактов конкретных topic version.
- `bootstrap-kafka-topics.sh` — repo-managed runtime helper, который materialize-ит topics и DLQ из `topic-registry.json` в baseline Kafka runtime до старта event-driven сервисов.

## Что агент должен знать перед изменениями

- Любое несовместимое изменение payload означает новый topic version и новый schema file, а не правку существующего `*.v1.schema.json`.
- Headers `eventId`, `eventType`, `schemaVersion`, `sourceService`, `correlationId`, `causationId`, `occurredAtUtc` считаются обязательным cross-service contract и не описываются внутри payload schema.
- Изменения здесь нужно синхронизировать с `platform/contracts/runtime/service-catalog.json`, архитектурными тестами и сервисами-производителями/потребителями.
- Любое tracked изменение в `platform/contracts/events/` должно приводить к rebuild EventBus-dependent service images через существующие `rules:changes`; deploy-only commit без такого rebuild не считается безопасным способом лечить stale runtime digests на production host.
- Даже документационное tracked изменение в этом каталоге считается shared contract change для CI/CD: следующий pipeline обязан пересобрать `build-identity`, `build-user`, `build-management`, `build-messenger`, `build-calls`, `build-gamification` и `build-analytics`, чтобы `deploy-prepare` мог опубликовать fresh refs для recovery rollout.

## Локальные команды/проверки

```bash
bash -n platform/contracts/events/bootstrap-kafka-topics.sh
python scripts/render-local.py --validate
```

## Связанные документы

- [../../README.md](../../README.md)
- [../../platform/README.md](../../platform/README.md)

Локальный runtime и запуск определены в [корневом README](../../../README.md).

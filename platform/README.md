# Platform

## Назначение

`platform/` хранит нейтральный shared layer репозитория `Collaby`: общие runtime-примитивы, internal contracts и repo-owned event schemas, которые разрешено переиспользовать между bounded contexts без зависимости на management-owned код.

## Что здесь является источником истины

- `platform/dotnet/Collaby.ServiceDefaults/` — источник истины для correlation, health/readiness conventions, OTEL wiring, internal auth и resilience helpers для query-only HTTP clients.
- `platform/dotnet/Collaby.EventBus/` — источник истины для repo-owned Kafka abstraction поверх `Confluent.Kafka`: typed publisher/consumer infrastructure, schema validation, DLQ publish и стандартные headers.
- `platform/dotnet/Collaby.InternalContracts/` — источник истины для shared DTO и typed payload records, которые не принадлежат конкретному runtime-сервису.
- `platform/contracts/events/` — источник истины для topic registry и versioned JSON Schemas repo-owned событий.
- `platform/contracts/system-administration/` — источник истины для tracked roster system-admin email-адресов, для env-backed `SYSTEM_ADMIN_EMAILS`.

## Что агент должен знать перед изменениями

- Новый межсервисный shared код сначала появляется в `platform/`, а не в `services/*`.
- `platform/` не владеет бизнес-доменом ни одного bounded context и не должен импортировать `*.Application`, `*.Domain` или инфраструктурный код конкретного сервиса.
- Legacy каталог `services/ManagementService/ManagementService.Platform/` больше не является shared source of truth для runtime-сервисов. Если общий примитив всё ещё нужен нескольким сервисам, он переносится в `platform/dotnet/*`.
- `IntegrationEventEnvelopeV1` удалён из runtime-контрактов. Канонический event contract теперь состоит из typed Kafka topics, JSON Schema из `platform/contracts/events/` и payload records из `platform/dotnet/Collaby.InternalContracts/Events/`.
- Если service Dockerfile собирает .NET сервис с `ProjectReference` на `platform/dotnet/Collaby.EventBus/Collaby.EventBus.csproj`, он обязан копировать в build-контекст `.csproj`, исходники `platform/dotnet/Collaby.EventBus/` и `platform/contracts/events/` до `dotnet restore` и `dotnet build`; иначе registry-build в CI соберёт сервис без embedded topic registry и schema resources.
- Любое изменение в `platform/contracts/events/` нужно синхронизировать с `platform/contracts/runtime/service-catalog.json`, архитектурными тестами и README тех сервисов, которые публикуют или потребляют соответствующие события.
- Любое изменение в `platform/contracts/system-administration/roster.json` нужно синхронизировать с `scripts/render-local.py` и README сервисов, которые потребляют env-backed admin/access roster.

## Локальные команды/проверки

```bash
dotnet build platform/dotnet/Collaby.ServiceDefaults/Collaby.ServiceDefaults.csproj -nologo
dotnet build platform/dotnet/Collaby.EventBus/Collaby.EventBus.csproj -nologo
dotnet build platform/dotnet/Collaby.InternalContracts/Collaby.InternalContracts.csproj -nologo
```

## Связанные документы

- [../README.md](../README.md)
- [../AGENTS.md](../AGENTS.md)
- [contracts/events/README.md](contracts/events/README.md)
- [contracts/system-administration/README.md](contracts/system-administration/README.md)
- [../services/ManagementService/README.md](../services/ManagementService/README.md)

Локальный runtime и запуск определены в [корневом README](../README.md).

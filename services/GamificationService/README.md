# GamificationService

## Назначение

`GamificationService` владеет bounded context геймификации: XP, уровни, квесты, streak-механика, leaderboard, магазин наград, skill tree и журналирование gamification events.

## Что здесь является источником истины

- [Program.cs](Program.cs) — состав приложения, auth/internal API, SignalR и Kafka/runtime registrations.
- [Infrastructure/Data/GamificationDbContext.cs](Infrastructure/Data/GamificationDbContext.cs) — PostgreSQL-модель и конфигурация сущностей.
- `Application/Services/ManagementTaskEventProcessor.cs` и `ManagementTaskEventsConsumer.cs` — typed consume `management.task-events.v1`.
- `Application/Services/ManagementProjectProgressSnapshotConsumer.cs`, `ProjectProgressProjectionWriter.cs`, `ProjectProgressBootstrapClient.cs` и `ProjectProgressBootstrapService.cs` — local projection `project_progress_snapshots`, bootstrap import и дальнейшая синхронизация progress snapshot через Kafka.
- `Application/Services/AnalyticsRewardOutbox.cs` и `AnalyticsOutboxDispatcher.cs` — typed publish `gamification.progress-events.v1`.
- `Application/Services/AdminUserDeletionCommandConsumer.cs` и `AdminUserDeletionPurgeService.cs` — Kafka purge consumer и result publisher.
- `Controllers/ProgressController.cs` — public progress surface, который читает только локальную projection, а не `ManagementService`.

## Что агент должен знать перед изменениями

- Публичный HTTP-контракт живёт под `/api/gamification/*`, realtime-контракт — под `/api/gamification/hubs/gamification`.
- Источником истины остаётся PostgreSQL `collaby_gamification`; Redis — кеш и ускоритель выборок, Mongo — журнал событий и аудит.
- `GamificationService` больше не принимает business events через internal HTTP route. Канонический вход для reward logic — Kafka topic `management.task-events.v1`.
- Project progress больше не читается на request path из `ManagementService`. Сервис держит локальную projection `project_progress_snapshots`, наполняет её через `management.project-progress-snapshot.v1`, а `GET /api/management/internal/v1/queries/projects/progress/export` использует только для initial seed пустой таблицы.
- Публичный `GET /api/gamification/progress/project/{projectId}` не должен шуметь `404` на пустом проекте: при отсутствии local snapshot сервис возвращает zeroed `ProjectProgressResponse`, а не ошибку.
- При изменениях алгоритмов нельзя ломать идемпотентность event processing, связь между global/project XP и правила выдачи наград.
- Аналитический fan-out теперь тоже идёт через Kafka: сервис публикует `gamification.progress-events.v1`, а не вызывает `AnalyticsService` по HTTP.
- `AnalyticsService` входит в baseline runtime, поэтому analytics fan-out и reward outbox не должны иметь optional-веток по runtime env.
- User hard delete теперь приходит через `admin.user-deletion-commands.v1`; повторная обработка должна оставаться идемпотентной и публиковать `completed|noop|failed` result.

## Локальные команды/проверки

```bash
dotnet build services/GamificationService/GamificationService.csproj -nologo
dotnet test services/GamificationService/GamificationService.Tests/GamificationService.Tests.csproj -nologo
python scripts/render-local.py --validate
```

## Связанные документы

- [../../README.md](../../README.md)
- [../../platform/README.md](../../platform/README.md)
- [../ManagementService/README.md](../ManagementService/README.md)
- [../AnalyticsService/README.md](../AnalyticsService/README.md)
- [../../database/README.md](../../database/README.md)

Локальный runtime и запуск определены в [корневом README](../../README.md).

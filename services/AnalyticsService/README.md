# AnalyticsService

## Назначение

`AnalyticsService` владеет read/reporting-сценариями Collaby и собирает аналитические выборки поверх локальных projection tables и `AnalyticsInboxEvents`.

## Что здесь является источником истины

- [Program.cs](Program.cs) — состав ASP.NET Core приложения, DI, auth, Kafka/runtime registrations и typed internal clients.
- [Controllers/AnalyticsSummaryController.cs](Controllers/AnalyticsSummaryController.cs) — публичный HTTP-контракт `/api/analytics/*`.
- [Application/Services/ProjectAnalyticsOverviewService.cs](Application/Services/ProjectAnalyticsOverviewService.cs) — сборка `overview` из локальных проекций, inbox-ивентов и upstream snapshot/user lookup.
- [Application/Services/ManagementProjectAnalyticsSnapshotClient.cs](Application/Services/ManagementProjectAnalyticsSnapshotClient.cs) — management-owned snapshot для access-check, status distribution и member roster.
- [Application/Services/UserLookupClient.cs](Application/Services/UserLookupClient.cs) — обогащение member directory профилями пользователей.
- [Application/Services/AnalyticsProjectionService.cs](Application/Services/AnalyticsProjectionService.cs) — Kafka ingest path для аналитических проекций.

## Что агент должен знать перед изменениями

- `AnalyticsService` не владеет текущим board-state и member roles. Источник истины для `statusDistribution` и project access — internal query `ManagementService`.
- `AnalyticsService` не владеет XP/level leaderboard. Канонический leaderboard остаётся в `GamificationService`; analytics только отдаёт `memberDirectory` для frontend enrichment.
- Канонический ingest остаётся Kafka-only через `management.task-events.v1` и `gamification.progress-events.v1`. Не возвращайте сервис к internal HTTP ingest surface.
- Публичный read-контракт включает:
  - `GET /api/analytics/projects/{projectId}/overview?period=week|month`
  - `GET /api/analytics/projects/{projectId}/summary`
  - `GET /api/analytics/users/{userId}/summary`
- `projects/{projectId}/overview` и `projects/{projectId}/summary` обязаны проходить project access check через management snapshot. `users/{userId}/summary` разрешён только для self-only сценария.
- `overview` возвращает `projectId`, `generatedAtUtc`, `summary`, `productivity`, `statusDistribution`, `memberDirectory`.
- `productivity` строится по UTC-дням за 7 или 30 дней и агрегирует только `task-completed`, `task-reopened`, `subtask-completed`, `comment-added`.
- При недоступности `UserService` member directory не должен ломаться: запись остаётся с `userId` и `role`, а user-поля деградируют в `null`.
- Runtime для `api-analytics` требует `ManagementService__BaseUrl`, `ManagementService__CallerService`, `ManagementService__InternalApiKey`, `UserService__BaseUrl`, `UserService__CallerService`, `UserService__InternalApiKey`.
- Demo seeds теперь создают analytics drill через public API. Исторические timestamps не backfill-ятся, поэтому seeded productivity нормально концентрируется в текущем UTC-дне.

## Локальные команды/проверки

```bash
dotnet build services/AnalyticsService/AnalyticsService.csproj -nologo
dotnet test services/AnalyticsService/AnalyticsService.Tests/AnalyticsService.Tests.csproj -nologo
```

## Связанные документы

- [../../README.md](../../README.md)
- [../ManagementService/README.md](../ManagementService/README.md)
- [../GamificationService/README.md](../GamificationService/README.md)
- [../IdentityService/README.md](../IdentityService/README.md)

Локальный runtime и запуск определены в [корневом README](../../README.md).

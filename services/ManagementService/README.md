# ManagementService

## Назначение

`ManagementService` владеет workspace/project/task/comment/invitation/notification-доменом, system-admin backoffice API и management-owned read/query surface для остальных bounded contexts.

## Что здесь является источником истины

- [Program.cs](/c:/Collaby/services/ManagementService/Program.cs) — runtime host, auth pipeline, Kafka/runtime registrations и internal auth wiring.
- [Controllers/InternalProjectsController.cs](/c:/Collaby/services/ManagementService/Controllers/InternalProjectsController.cs) — query-only internal surface `/api/management/internal/v1/queries/projects/*`.
- [Application/Services/ProjectProgressSnapshotBuilder.cs](/c:/Collaby/services/ManagementService/Application/Services/ProjectProgressSnapshotBuilder.cs) — canonical project progress/export builder.
- [Application/Services/OutboxGamificationHook.cs](/c:/Collaby/services/ManagementService/Application/Services/OutboxGamificationHook.cs) и `GamificationOutboxDispatcher.cs` — canonical publish `management.task-events.v1`.
- [scripts/seed-demo-data.sh](/c:/Collaby/scripts/seed-demo-data.sh) и [scripts/seed-demo-data.ps1](/c:/Collaby/scripts/seed-demo-data.ps1) — demo-seed сценарии через public management API, которые дополнительно создают deterministic AI hidden/task-context через `/api/ai/mentor/context`.

## Что агент должен знать перед изменениями

- `ManagementService` остаётся owner текущего board-state, task status distribution и workspace member roles. `AnalyticsService` и другие downstream consumers читают это только через internal query.
- Startup migrations обязаны переживать legacy schema drift на production-хосте. Если колонка или таблица уже были созданы repair/manual path'ом, ранние EF migrations должны оставаться идемпотентными и не валить readiness на `MigrateAsync`.
- Internal analytics snapshot контракт:
  - `GET /api/management/internal/v1/queries/projects/{projectId}/analytics-snapshot?userId={userId}`
  - response содержит `projectId`, `workspaceId`, `hasAccess`, `statusDistribution[]`, `members[]`
  - `members[]` включает только активных участников workspace и отдаёт `userId` + `role`
- `statusDistribution` для analytics нельзя пересчитывать в downstream сервисах по собственным правилам. Источник истины — snapshot из `ManagementService`.
- Business events продолжают публиковаться через Kafka topic `management.task-events.v1`; не возвращайте HTTP fan-out и не пишите в downstream базы напрямую.
- Seeds должны работать только через public API. Для analytics drill допустимы реальные task moves, comments и subtasks, но не прямые записи в БД и не искусственный backdating.
- Demo seeds теперь обязаны оставлять project state, в котором:
  - есть несколько task statuses для непустого `statusDistribution`
  - есть события `task-completed`, `task-reopened`, `subtask-completed`, `comment-added` для `/analytics`
- Тот же demo-seed теперь обязан создавать несколько approved AI context items для текущего workspace/project, чтобы `/assistant` имел воспроизводимый внутренний контекст даже без ручного mentor-ingest.
- `AnalyticsService` входит в baseline runtime и является штатным internal caller для management analytics snapshot.
- Invitation lifecycle остаётся management-owned контрактом:
  - `POST /api/management/Workspaces/{workspaceId}/invitations` создаёт email-based invite и возвращает token только в create-response;
  - `GET /api/management/invitations/{token}` обязан отдавать достаточно данных для публичного preview/accept flow во frontend, включая `workspaceId`, `workspaceName`, `email`, `role`, `status` и сроки действия;
  - `POST /api/management/invitations/accept` остаётся единственной canonical точкой принятия invite после frontend preview.
- `Application/Services/EmailQueueService.cs` действительно отправляет SMTP-письмо приглашения. Ссылка использует `Frontend:BaseUrl` и маршрут `/accept-invitation?token=...`; токен не пишется в лог. Параметры `Email:SmtpHost`, `Email:SmtpPort`, `Email:UseTls` и необязательные username/password поддерживают Mailpit без TLS/AUTH и внешний SMTP.
- `scripts/seed-local.py` восстанавливает прерванное создание демопользователей и приглашений, получает token из письма Mailpit и проверяет canonical accept API. Демоданные повторно не создаются.

Перезапуск сервисов `/admin/ops/restart-operations` сохраняет операцию и возвращает `202` до обращения в Kubernetes API. `ServiceRestartWorker` восстанавливает незавершённые операции после перезапуска `workspace`; GET читает сохранённое состояние. Перед PATCH сохраняются namespace и UID Deployment, аннотация `collaby.io/restart-operation` содержит UUID операции. Повторный запрос для активного target возвращает `409` (включая гонку через частичный уникальный индекс). Готовность требует observedGeneration и всех доступных реплик; API допускает только access/workspace/realtime/engagement/assistant. Права задаются Kubernetes Role, без доступа к секретам и хранилищам.

## Локальные команды/проверки

`ManagementService.Tests/SwaggerGenerationTests.cs` проверяет HTTP-генерацию семи .NET Swagger-документов на реальных сборках контроллеров и combined-host conventions без запуска БД/Kafka. Центральный `Microsoft.OpenApi` должен оставаться совместимым с ABI установленного Swashbuckle; для `Swashbuckle.AspNetCore 10.2.1` используется `Microsoft.OpenApi 2.7.5`. Успешная компиляция с OpenAPI.NET 3.x не доказывает работоспособность Swagger JSON.

```bash
dotnet build services/ManagementService/ManagementService.csproj -nologo
dotnet test services/ManagementService/ManagementService.Tests/ManagementService.Tests.csproj -nologo
dotnet ef database update --project services/ManagementService/ManagementService.csproj --startup-project services/ManagementService/ManagementService.csproj
bash scripts/seed-demo-data.sh
powershell -ExecutionPolicy Bypass -File scripts/seed-demo-data.ps1
```

## Связанные документы

- [../../README.md](../../README.md)
- [../AnalyticsService/README.md](../AnalyticsService/README.md)
- [../GamificationService/README.md](../GamificationService/README.md)
- [../IdentityService/README.md](../IdentityService/README.md)

Локальный runtime и запуск определены в [корневом README](../../README.md).

# UserService

## Назначение

`UserService` владеет пользовательскими профилями, preferences, onboarding и activity. Сервис не владеет аутентификацией и не хранит roster system admins.

## Что здесь является источником истины

- `services/UserService/Program.cs` — public/internal HTTP surface, Kafka/runtime registrations и DI-контракт сервиса.
- `services/UserService/Controllers/` — публичные и internal-only endpoints.
- `services/UserService/Infrastructure/Data/UserDbContext.cs` и доменные сущности рядом — профильные данные, которыми владеет сервис.
- `services/UserService/Contracts/Requests/UpdatePreferencesRequest.cs` и `services/UserService/Contracts/Responses/UserPreferencesDto.cs` — source of truth для публичного preferences-контракта, включая account-level `defaultBoardBackground`.
- `services/UserService/Application/Services/IdentityDirectoryBootstrapClient.cs`, `IdentityDirectoryBootstrapService.cs` и `IdentityUserDirectoryConsumer.cs` — bootstrap-only import и дальнейшая синхронизация auth-directory facts через `identity.user-directory.v1`.
- `services/UserService/Infrastructure/Data/Migrations/20260315195900_AddDefaultBoardBackgroundPreference.cs` — текущая repo-managed миграция для хранения `DefaultBoardBackgroundJson`; новую миграцию добавляйте только при реальном model drift.
- `services/UserService/appsettings.json` — runtime контракт сервиса.
- Корневой [README.md](../../README.md) — глобальные runtime, CI/CD и bounded-context инварианты.

## Что агент должен знать перед изменениями

- Публичный API живёт под `/api/users/*`; sync query-only surface для межсервисных вызовов — под `/api/users/internal/v1/queries/*`.
- `UserService` не должен дублировать auth-логику `IdentityService`, но и не должен тянуть её на request path по HTTP. Auth-directory facts материализуются в локальную projection `identity_directory_entries` через `identity.user-directory.v1`.
- `GET /api/auth/internal/v1/queries/users/directory/export` допустим только как bootstrap-only seed, когда локальная projection пуста. После initial import request-path fallback в `IdentityService` запрещён.
- Startup `UserService` остаётся синхронным и stage-aware: сначала применяются PostgreSQL migrations, затем bootstrap'ится projection `identity_directory_entries`. Пока любая из этих стадий не завершена, `/api/users/health/ready` и `/api/users/health` обязаны возвращать `503` с конкретной причиной текущей стадии, а не с общим `Startup checks have not completed yet`.
- Bootstrap `identity_directory_entries` теперь использует тот же retry/backoff budget, что и миграции. Если projection пуста и bootstrap временно не может достать export из `IdentityService`, сервис остаётся unready, но продолжает публиковать stage-aware причину в readiness body.
- Admin hard delete больше не использует HTTP purge route. Сервис потребляет `admin.user-deletion-commands.v1`, удаляет profile/preferences/onboarding/activity по `UserId` и публикует `admin.user-deletion-results.v1`.
- Purge semantics для `UserService` должны оставаться идемпотентными: повторная обработка возвращает `noop` или эквивалентный успешный result.
- Сервис по-прежнему обязан требовать service-scoped internal auth только на query HTTP surface; deletion saga идёт через Kafka и не использует internal purge endpoint.
- Preferences-контракт включает `defaultBoardBackground` и reset-флаг для account-level фона. `UserService` хранит только account default и не знает о project override.
- Каноническое правило effective board background собирается на frontend как `project override ?? account default`; `UserService` владеет только persisted account preference и не должен тащить project metadata в свой контракт.
- Для этой области не добавляются новые route families: account-level фон живёт внутри существующего preferences/update path.

## Локальные команды/проверки

```bash
dotnet build services/UserService/UserService.csproj -nologo
dotnet test services/UserService/UserService.Tests/UserService.Tests.csproj -nologo
python scripts/render-local.py --validate
```

## Связанные документы

- [../../README.md](../../README.md)
- [../../platform/README.md](../../platform/README.md)
- [../IdentityService/README.md](../IdentityService/README.md)
- [../ManagementService/README.md](../ManagementService/README.md)
- [../../database/README.md](../../database/README.md)

Локальный runtime и запуск определены в [корневом README](../../README.md).

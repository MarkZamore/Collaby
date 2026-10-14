# IdentityService

## Назначение

`IdentityService` владеет login/register/refresh/logout, JWT, refresh tokens, email verification, password reset, Google Sign-In и system-admin user governance для Admin Panel.

## Что здесь является источником истины

- `services/IdentityService/Program.cs` — host, DI, auth pipeline, runtime registrations и bind configuration.
- `services/IdentityService/Controllers/AuthController.cs` — публичный auth surface `/api/auth/*`, включая `GET /api/auth/google/config`, `POST /api/auth/google`, `POST /api/auth/google/register`, `POST /api/auth/validate-reset-password-token` и trusted admin bootstrap `POST /api/auth/access-login`.
- `services/IdentityService/Application/Services/AuthService.cs` — каноническая оркестрация login/register/refresh/logout, Google auth, autolink по email и выдача auth session.
- `services/IdentityService/Application/Services/GoogleOAuthService.cs` — серверная валидация Google ID token через `GoogleJsonWebSignature.ValidateAsync`.
- `services/IdentityService/Application/Services/GoogleRegistrationTokenService.cs` — signed registration token для Google-first onboarding без отдельной БД-таблицы.
- `services/IdentityService/Application/Services/GoogleNicknameGenerator.cs` — серверная нормализация и fallback-подсказка никнейма для новых Google users.
- `services/IdentityService/Application/Settings/GoogleOptions.cs` и `services/IdentityService/appsettings.json` — source of truth для `Google:ClientId`.
- `services/IdentityService/Infrastructure/Data/IdentityDbContext.cs` — схема `users`, `refresh_tokens`, `oauth_accounts`, outbox state и deletion state.
- `services/IdentityService/Application/Services/IdentityDirectoryOutboxWriter.cs` и `IdentityDirectoryOutboxDispatcher.cs` — canonical outbox/publish path для `identity.user-directory.v1`.
- `services/IdentityService/Controllers/InternalUsersController.cs` — internal query/export surface, включая `GET /api/auth/internal/v1/queries/users/directory/export`.
- `platform/dotnet/Collaby.ServiceDefaults/SystemAdministration/` — shared env-backed system-admin contract.

## Что агент должен знать перед изменениями

- Единственный tracked source of truth для roster system admins — `platform/contracts/system-administration/roster.json`; runtime по-прежнему читает только env `SYSTEM_ADMIN_EMAILS`.
- Policy `SystemAdmin` определяется только email claim против env roster, а не workspace ролями.
- Runtime-контур обязан прокидывать `SYSTEM_ADMIN_EMAILS` в `access`; локальный roster включает `admin@collaby.local`. Пустая настройка ломает `SystemAdmin` claim и `user.isSystemAdmin`.
- Registry-build Dockerfile обязан копировать `platform/contracts/runtime/service-catalog.json` в build context, потому что `IdentityService.csproj` линкует catalog в output для purge-target contract.
- Auth payload `login`, `refresh` и `me` обязан возвращать `user.isSystemAdmin`; access token обязан нести claim `SystemAdmin`.
- SMTP использует `Email:SmtpHost`, `Email:SmtpPort`, `Email:UseTls` и необязательные username/password. Локальный Mailpit принимает SMTP без AUTH и TLS на порту 1025; для внешнего SMTP TLS включается явно.
- Все ссылки verify/reset строятся из `Frontend:BaseUrl`. Локальный URL — `http://localhost:8080`; Mailpit API используется seed для настоящего подтверждения регистрации.
- Refresh-cookie использует HttpOnly, SameSite=Strict и Path=/ для выдачи и удаления. `Cookies:Secure` задаёт runtime; без явной настройки используется `Request.IsHttps`.
- Google Sign-In реализован только как Google Identity Services button flow с клиентским `credential` и серверной валидацией Google ID token. В этом контуре не используются `client_secret`, redirect URI и authorization-code exchange.
- Единственный source of truth для Google web client ID — `Google:ClientId` / `GOOGLE_CLIENT_ID` в `api-identity`; `frontend` не должен дублировать этот контракт через `VITE_GOOGLE_CLIENT_ID`.
- `POST /api/auth/google` либо возвращает обычную authenticated session, либо `requiresNickname=true` + signed `registrationToken` на 10 минут; `POST /api/auth/google/register` завершает Google-first регистрацию после одноразового выбора никнейма.
- Frontend reset-password flow обязан сначала вызывать `POST /api/auth/validate-reset-password-token`; used/expired token не должен доходить до рабочей формы смены пароля и должен отсеиваться до submit.
- Если verified Google email совпадает с существующим локальным пользователем, `IdentityService` автоматически создаёт `oauth_accounts` link, поднимает `IsEmailVerified=true` и не меняет существующий `Username`.
- Dedicated admin flow `POST /api/auth/access-login` остаётся отдельным trusted bootstrap для `admin.<zone>` и не должен смешиваться с public Google login.
- Trusted admin bootstrap на `admin.<zone>` обязан корректно определять dedicated host даже за reverse proxy: `AuthController` должен учитывать `X-Forwarded-Host` и `X-Original-Host`.
- Protected allowlisted accounts нельзя deactivate или hard-delete, пока их email остаётся в `SYSTEM_ADMIN_EMAILS`.
- Hard delete пользователя — только async tracked cascade через `UserDeletionOperation` и `UserDeletionOperationStep`.
- Повторный `POST /admin/users/{userId}/hard-delete` для legacy/stale `pending|running|retry_requested` операции больше не обязан навсегда возвращать `409`: после таймаута stale-operation считается recoverable и переводится в `retry_requested`, чтобы background orchestrator мог перепубликовать purge-команды.
- `POST /admin/users/deletion-operations/{operationId}/retry` теперь предназначен не только для `failed`, но и для stale in-flight операций; свежие активные операции по-прежнему не должны ретраиться вручную.
- `IdentityService` больше не делает HTTP fan-out purge. Он публикует `admin.user-deletion-commands.v1` и завершает операцию только после terminal `completed|noop` результатов из `admin.user-deletion-results.v1`.
- Auth-directory facts для downstream сервисов больше не читаются через request-path lookup. `IdentityService` публикует `identity.user-directory.v1` через local outbox, а `GET /api/auth/internal/v1/queries/users/directory/export` разрешён только как bootstrap-only seed для пустой projection.
- Auth-side user record удаляется только после успешного завершения всех обязательных purge step-ов.
- `UserDeletionOperation` и `UserDeletionOperationStep` обязаны переживать удаление auth-side user record; это saga/audit state для Admin Panel, а не дочерняя таблица `users`.
- Source of truth для enabled deletion targets — `service-catalog.json` плюс `AnalyticsService:Enabled`; endpoint URLs и internal API keys больше не управляют purge flow.

## Локальные команды/проверки

```bash
dotnet build services/IdentityService/IdentityService.csproj -nologo
dotnet test services/IdentityService/IdentityService.Tests/IdentityService.Tests.csproj -nologo
dotnet ef database update --project services/IdentityService/IdentityService.csproj --startup-project services/IdentityService/IdentityService.csproj
```

## Связанные документы

- [../../README.md](../../README.md)
- [../../platform/README.md](../../platform/README.md)
- [../../platform/contracts/system-administration/README.md](../../platform/contracts/system-administration/README.md)
- [../UserService/README.md](../UserService/README.md)
- [../ManagementService/README.md](../ManagementService/README.md)

Локальный runtime и запуск определены в [корневом README](../../README.md). `scripts/seed-local.py` создаёт демопользователей через API, проверяет письма Mailpit и refresh/logout; повторный запуск сохраняет существующие сущности.

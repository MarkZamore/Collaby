# MessengerService

## Назначение

`MessengerService` владеет чатами, сообщениями, presence-состоянием и realtime-доставкой сообщений.

## Что здесь является источником истины

- [`services/MessengerService/Program.cs`](Program.cs) — состав HTTP API, SignalR и runtime-конфигурации.
- Доменные и application-слои внутри `services/MessengerService/` — правила обработки чатов, сообщений и presence.

## Что агент должен знать перед изменениями

- Публичный HTTP-контракт живёт под `/api/messenger/*`, а публичный hub — под `/api/messenger/hubs/chat`.
- `DELETE /api/messenger/chats/{chatId}` — часть публичного HTTP-контракта: для private-чата hard delete может выполнить только создатель чата (`CreatedBy`), для project/channel-чата — только admin-участник; удаление убирает и чат, и связанные сообщения.
- `POST /api/messenger/chats/{chatId}/clear` — очищает историю сообщений без удаления самого чата: для private-чата доступно участнику чата, для project/channel-чата — только admin-участнику; при очистке `LastMessage` сбрасывается в `null`.
- Источником истины для сообщений и чатов выступает MongoDB `collaby_messenger`; Redis используется для presence и кеширующих сценариев.
- Обогащение пользовательских данных должно идти только через internal HTTP-контракты `UserService`, а не через общие таблицы или прямой доступ к чужим БД.
- Изменения SignalR payload и маршрутов считаются внешним контрактом для frontend.

## Локальные команды/проверки

```bash
dotnet build services/MessengerService/MessengerService.csproj -nologo
dotnet test services/MessengerService/MessengerService.Tests/MessengerService.Tests.csproj -nologo
python scripts/render-local.py --validate
```

## Связанные документы

- [`README.md`](../../README.md)
- [`services/UserService/README.md`](../UserService/README.md)
- [`database/README.md`](../../database/README.md)

Локальный runtime и запуск определены в [корневом README](../../README.md).

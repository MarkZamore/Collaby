# Frontend

## Назначение

React SPA и Caddy для локального сайта `http://localhost:8080`. API и WebSocket используют один origin.

## Что здесь является источником истины

- `vite.config.js` — сборка, тесты и proxy для разработки.
- `Caddyfile` — маршруты API, WebSocket и Swagger, кеширование ассетов.
- `Dockerfile` — runtime-образ; `scripts/` — метаданные сборки.

## Что агент должен знать перед изменениями

Vite на порту 3000 проксирует запросы через запущенный gateway `http://localhost:8080`; другой адрес задаётся через `VITE_PROXY_TARGET`. Swagger доступен на `/swagger/`. Его схемы остаются в backend-сервисах.

Ассеты с хешем кешируются; отсутствующий asset возвращает 404. `runtime-version.json` и SPA HTML не кешируются, чтобы frontend обнаруживал новую сборку. Локальные настройки и секреты не включаются в frontend-образ.

## Локальные команды/проверки

```powershell
cd frontend
npm ci
npm test
npm run build
```

## Связанные документы

[Корневой README](../README.md), [Панели](../panels/README.md).

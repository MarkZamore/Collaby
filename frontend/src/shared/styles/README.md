# Shared Styles

## Назначение

Этот каталог содержит общие CSS-слои frontend-приложения: глобальные стили, вспомогательные классы и стили отдельных общих элементов.

## Что здесь является источником истины

- `global.css` — базовый слой глобальных переменных и общих правил.
- `semantic.css` — общий semantic-layer для приложенческих примитивов (`glass-panel`, toolbar/control, floating menu, modal shell, admin surfaces), который импортируется в default runtime.
- `helpers.css` — opt-in helper-слой, который не входит в default runtime import и нужен только если поверх utility-first слоя действительно требуются прикладные классы.
- `Button.css`, `ThemeToggle.css` и другие локальные CSS-файлы — источник истины для стилистики соответствующих shared-компонентов.
- `frontend/src/index.css` — источник истины для подключения Tailwind CSS v4 и CSS-first styling-контура.

## Что агент должен знать перед изменениями

- Этот каталог должен описывать устойчивые style-contracts, а не служить временным местом для случайного CSS.
- Если стиль относится только к одному экрану или feature, его лучше держать ближе к компоненту, а не в `shared/styles`.
- При изменении глобальных токенов нужно проверять влияние на весь frontend, а не только на один экран.
- Default runtime слой для Tailwind идёт через `frontend/src/index.css`; `global.css` и `semantic.css` подключаются там же, а `helpers.css` не должен становиться неявным источником истины для utility-классов приложения.
- App-local CSS tokens в `global.css` должны использовать собственный namespace (`--app-*`, `--landing-*`), чтобы не переопределять theme vars Tailwind v4 вроде `--radius-*`, `--shadow-*` и `--blur-*`.
- Любые устаревшие visual reference-артефакты и legacy styling-path не должны возвращаться в runtime: текущий source of truth для общего визуального языка живёт только в `index.css`, `global.css` и `semantic.css`.

## Локальные команды/проверки

```bash
cd frontend
npm run build
```

## Связанные документы

- [../../../../README.md](../../../../README.md)
- [../../../README.md](../../../README.md)

Локальный runtime и запуск определены в [корневом README](../../../../README.md).

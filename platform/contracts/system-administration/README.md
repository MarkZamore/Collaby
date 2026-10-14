# system-administration

## Назначение

Контракт системных администраторов. Runtime читает SYSTEM_ADMIN_EMAILS; локальный renderer задаёт admin@collaby.local. Cloudflare Access не участвует в локальном входе.

## Что здесь является источником истины

[../../../kubernetes/runtime-spec.json](../../../kubernetes/runtime-spec.json), [../../../scripts/render-local.py](../../../scripts/render-local.py) и файлы этого каталога.

## Что агент должен знать перед изменениями

Изменения должны сохранять изоляцию данных модулей и локальную авторизацию. Конфигурация материализуется в ConfigMap и Secret; пароли хранятся только в исключённом `.local`. Runtime-процедуру определяет корневой README.

## Локальные команды/проверки

Из корня: `python scripts/render-local.py --validate`, `python scripts/verify-runtime.py`, `start-local.cmd`.

## Связанные документы

[Корневой README](../../../README.md).

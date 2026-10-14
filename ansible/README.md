# ansible

## Назначение

Применение локального контура через `kubernetes.core` из одноразового Job в `collaby-system`. SSH, systemd и настройка Ubuntu в этом контуре отсутствуют.

## Что здесь является источником истины

`Dockerfile` фиксирует контроллер, `local.yml` задаёт порядок применения ресурсов и проверку готовности. Job получает конфигурацию и credentials через отдельные ConfigMap и Secret.

## Что агент должен знать перед изменениями

Порядок: конфигурация/PVC → PostgreSQL/MongoDB/Redis/Kafka/Mailpit → готовность StatefulSet → Kafka topics Job → backend с миграциями → AI → frontend → панели → Portainer → API seed. Secret-задачи и seed используют `no_log`. Topics Job имеет deadline, backoff и TTL; основной Job создаёт launcher с deadline и TTL. Повторное применение сохраняет PVC и данные.

## Локальные команды/проверки

`docker build -f ansible/Dockerfile -t collaby/controller:check .`

`docker run --rm collaby/controller:check ansible-playbook ansible/local.yml --syntax-check`

## Связанные документы

[Корневой README](../README.md), [Kubernetes](../kubernetes/README.md).


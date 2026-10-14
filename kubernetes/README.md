# kubernetes

## Назначение

Локальный одноузловой runtime Docker Desktop kubeadm.

## Что здесь является источником истины

`runtime-spec.json` содержит образы, environment и mounts. `../scripts/render-local.py` создаёт ресурсы. `bootstrap.yml` задаёт namespaces и RBAC. `config/alloy.alloy` собирает pod logs через Kubernetes API.

## Что агент должен знать перед изменениями

PostgreSQL, MongoDB, Redis и Kafka — StatefulSet с PVC; MongoDB сохраняет `/data/db` и `/data/configdb`. AI и панели используют отдельные PVC. Выбор StorageClass оставлен default Docker Desktop. Локальные образы имеют content tags и `imagePullPolicy: Never`.

Portainer использует namespace `portainer` и локальную Kubernetes environment. Workspace имеет только get/patch для пяти разрешённых Deployment. Alloy читает pods/log в трёх namespaces, не получает host mounts. Netdata parent `server` сохраняет историю и собирает Kubernetes state; child DaemonSet читает proc/sys/cgroup Linux-узла и передаёт метрики parent по сгенерированному stream key. Windows host не является наблюдаемым узлом Netdata.

Grafana, pgAdmin, Mongo Express, Kafka UI и Portainer имеют обычный вход. RedisInsight, Mailpit и Netdata открываются через Caddy basic_auth. Локальные credentials находятся только в `.local/rendered/local.json`. Телеметрия имеет ограниченное хранение; Prometheus дополнительно ограничен 512MB.

`cloudflare.yml` исключён из обычного запуска. Его применение требует отдельной настройки зоны `collaby.org` и tunnel Secret.

## Локальные команды/проверки

`python scripts/render-local.py --validate` создаёт `.local/validated` без чтения external credentials и изменения конфигурации запуска.

`python -m unittest discover -s scripts/tests`

`kubectl --context docker-desktop -n collaby get pods,pvc`

## Связанные документы

[Корневой README](../README.md), [Ansible](../ansible/README.md), [Cloudflare](../cloudflare/README.md).

pgAdmin server definitions материализуются renderer из `POSTGRES_*_USER`. Соответствующие пароли находятся в Secret `pg-connections`; initContainer под UID 5050 копирует `.pgpass` в пользовательский storage с правами 0600. ConfigMap содержит только публичные параметры подключения. Повторный запуск обновляет server definitions без дубликатов и не требует ручного ввода паролей PostgreSQL.

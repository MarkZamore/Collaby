# Collaby

## Назначение

Локальный контур совместной работы: React frontend, .NET 10 backend, realtime и CPU AI с обученным GGUF-адаптером. Исходная версия: `feature/shop`, коммит `a9816babbae12f2b92bfce45792ae6148248d4d2`. Git-история начинается заново.

## Что здесь является источником истины

- `start-local.cmd`, `stop-local.cmd`, `scripts/local.ps1` — запуск и остановка Windows.
- `kubernetes/runtime-spec.json`, `scripts/render-local.py`, `kubernetes/bootstrap.yml` — workloads, конфигурация и права.
- `ansible/local.yml` — применение конфигурации из одноразового Kubernetes Job.
- `platform/contracts/runtime/service-catalog.json` — каталог runtime.
- `services/AgentService/adapter-manifest.json` и `assets/` — адаптер с проверкой SHA-256.

## Что агент должен знать перед изменениями

Первичная подготовка требует WSL 2, Docker Desktop с Linux engine и Kubernetes kubeadm. Выполните `prepare-machine.cmd`, перезагрузите Windows при необходимости и создайте одноузловой кластер в Docker Desktop: Kubernetes → Create cluster → kubeadm. Launcher работает только с контекстом `docker-desktop`.

Настройки и пароли находятся в исключённом каталоге `.local`. Данные остаются в PVC после остановки. Первый запуск скачивает базовую AI-модель и требует интернета. Локальные образы получают теги по содержимому и не публикуются в registry.

`start-local.cmd` ожидает readiness, создаёт через API подтверждённых демопользователей и проект с задачами, затем открывает браузер. Повторный запуск использует сохранённые пароли и данные. Демоадминистратор — `admin@collaby.local`; пароль находится в `.local/rendered/local.json`, поле `DEMO_PASSWORD`. Панели используют локальную авторизацию. Mailpit принимает письма регистрации, приглашений и восстановления; наружная SMTP-отправка по умолчанию выключена.

`stop-local.cmd` останавливает workloads и принадлежащие launcher процессы port-forward, сохраняя PVC. Ошибки запуска и адреса панелей отображаются launcher; логи Job доступны через `kubectl --context docker-desktop -n collaby-system logs job/<имя>`.

Port-forward процессы контролируются launcher: повторный запуск использует существующий процесс после проверки PID и команды; оборванное соединение восстанавливается supervisor. Все опубликованные порты привязаны к loopback.

GitHub Actions выполняет .NET 10, Node 24 и Python 3.12 тесты, сборку девяти runtime-образов и Ansible-контроллера, проверку Kubernetes схем и публикуемого набора на секреты. Push запускает проверки и сборку; применение локальной конфигурации выполняет только launcher.

Cloudflare выключен. Его отдельная конфигурация требует явного выбора зоны `collaby.org`; Terraform state хранится в Kubernetes Secret, блокировка — Lease. Google OAuth требует разрешённого origin `http://localhost:8080`; внешние настройки хранятся только в `.local/external.json`. Netdata показывает Linux-узел Docker Desktop и workloads.

Сайт: `http://localhost:8080`, почта: `http://localhost:8025`, Grafana: `http://localhost:3001`, Portainer: `http://localhost:9000`, PostgreSQL: `http://localhost:5050`, MongoDB: `http://localhost:8081`, Redis: `http://localhost:5540`, Kafka: `http://localhost:8082`, Netdata: `http://localhost:19999`.

## Локальные команды/проверки

```powershell
.\start-local.cmd
.\stop-local.cmd
python scripts/render-local.py --validate
python scripts/verify-runtime.py
python -m unittest discover -s scripts/tests
python scripts/test-local-api.py
powershell -File scripts/test-launcher.ps1
dotnet build Collaby.slnx
dotnet test Collaby.slnx
```

## Связанные документы

[Правила изменений](AGENTS.md), [Ansible](ansible/README.md), [Kubernetes](kubernetes/README.md).

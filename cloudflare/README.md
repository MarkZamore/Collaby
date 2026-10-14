# Cloudflare

## Назначение

Опциональный публичный доступ к сайту. По умолчанию выключен и не применяется launcher.

## Что здесь является источником истины

`main.tf`, `variables.tf`, `versions.tf`, `backend-rbac.yml`, `plan-job.yml` и `kubernetes/cloudflare.yml`.

## Что агент должен знать перед изменениями

Нужны явные account_id, zone_id и zone_name=collaby.org. Terraform backend использует Kubernetes Secret и Lease в collaby-system. Токены хранятся только локально. Существующие DNS-записи следует импортировать перед применением; автоматическое удаление записей и tunnel не выполняется. Не публикуйте панели, Swagger или SSH через tunnel.

Legacy state из исходного HTTP backend хранится отдельно: Secret `tfstate-default-collaby-cloudflare-origin-archive` и Lease `lock-tfstate-default-collaby-cloudflare-origin-archive` в `collaby-system`. Его локальная резервная копия находится в исключённом `.local/cloudflare-state-backup/original.tfstate`. Этот state содержит прежние DNS и Access-ресурсы; не подключайте его к минимальному `main.tf` и не выполняйте apply из архивного backend. Для зоны `collaby.org` используется отдельный suffix `collaby-cloudflare`: это исключает удаление прежних ресурсов при включении нового компонента.

## Локальные команды/проверки

`terraform fmt -check cloudflare`; применение выполняется только после отдельной настройки публичного доступа.

Для проверки в кластере создайте Secret `cloudflare-terraform-inputs` в `collaby-system` из исключённого локального env-файла с ключами `CLOUDFLARE_API_TOKEN`, `TF_VAR_account_id`, `TF_VAR_zone_id`, `TF_VAR_zone_name=collaby.org`. Создайте ConfigMap `cloudflare-terraform-source` из `main.tf`, `variables.tf`, `versions.tf`, `outputs.tf` и `.terraform.lock.hcl`. Затем примените `backend-rbac.yml` и `plan-job.yml` через контекст `docker-desktop`. Job выполняет только `terraform plan` и не изменяет DNS.

Публичный доступ включается отдельно после проверки плана: выполните `terraform apply` внутри Job с теми же inputs/backend, сохраните sensitive output `tunnel_token` в Secret `cloudflare` namespace `collaby`, key `tunnel-token`, затем примените `kubernetes/cloudflare.yml`. Не выводите token в журналы. Terraform-права ограничены namespace `collaby-system`, не выдавайте их backend-сервисам.

## Связанные документы

[Корневой README](../README.md).

Локальный runtime и запуск определены в [корневом README](../README.md).

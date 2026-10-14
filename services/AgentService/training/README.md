# Обучение локального Qwen-ассистента

## Назначение

`services/AgentService/training/` хранит repo-managed training contour для `Qwen2.5-1.5B-Instruct`: подготовку датасета, Colab notebook для `SFT + QLoRA (4-bit)` и helper-скрипты для публикации GGUF adapter в GitHub Releases и обновления tracked manifest.

## Что здесь является источником истины

- `prepare_dataset.py` — источник истины для подготовки `mentor`/`assistant` SFT-датасета из `assistant.db`, JSON snapshot-ов проектов и live `assistant-snapshot` выборок из `ManagementService`.
- `qwen_qlora_colab.ipynb` — источник истины для запуска обучения в Google Colab.
- `package_colab_bundle.py` — источник истины для упаковки non-empty dataset, notebook и runtime manifest-ов в единый upload-ready bundle для Colab.
- `publish_release.py` публикует GGUF и `adapter-manifest.json` в `MarkZamore/Collaby` с тегом `adapter-<version>`; команда требует локальный `GITHUB_TOKEN`, который не сохраняется в манифесте. Существующий asset не перезаписывается. `publish_adapter_package.py` сохраняет совместимое имя команды.
- `export_adapter_manifest.py` — источник истины для финального manifest адаптера, когда download URL уже известен отдельно от шага публикации.
- [../model-manifest.json](../model-manifest.json) и [../adapter-manifest.json](../adapter-manifest.json) — tracked runtime contract для base model и активного GGUF LoRA-адаптера.

## Что агент должен знать перед изменениями

- Каталог не владеет production deploy напрямую: он готовит training artifacts, которые затем должны быть опубликованы и отражены в `services/AgentService/adapter-manifest.json`.
- Подготовка датасета должна оставаться `workspace-first`: mentor и assistant slice нельзя смешивать без метаданных `workspaceId`/`projectId`/`taskId`, если они доступны.
- `prepare_dataset.py` обязан принимать реальный `assistant.db` из `ai-state` и не зависеть от ручных ad-hoc CSV.
- Канонический путь для project snapshot-ов теперь двойной: либо `--snapshot-dir` с уже выгруженными JSON, либо `--project-id` вместе с `MANAGEMENT_SERVICE_URL`, `AI_INTERNAL_SERVICE_NAME` и `INTERNAL_AUTH_AI_TO_MANAGEMENT_KEY`, чтобы датасет можно было собирать прямо из живого internal API.
- Если одновременно переданы file snapshot-ы и live snapshot того же проекта, приоритет у последнего ответа internal API: training contour должен опираться на более свежий project state.
- Bundle для Colab должен собираться repo-managed скриптом, а не вручную: внутри архива обязаны лежать dataset JSONL, `model-manifest.json`, template текущего `adapter-manifest.json`, notebook и `export_adapter_manifest.py`.
- Bundle для Colab должен оставаться самодостаточным для handoff: внутри архива обязаны лежать и `publish_adapter_package.py`, и `asset_auth.py`, чтобы после Colab можно было опубликовать GGUF adapter без обращения к сторонним ad-hoc скриптам.
- Colab flow обязан заканчивать обучение конвертацией PEFT LoRA в GGUF через официальный `llama.cpp` путь (`convert_lora_to_gguf.py` или эквивалентный официальный инструмент); raw каталог `final-adapter/` или его `.zip` не считаются deploy-ready runtime артефактом.
- Notebook обязан оставаться совместимым с актуальным `trl` API: конфигурация `SFTTrainer` должна проходить через `SFTConfig`, а tokenizer/processor — через `processing_class`, чтобы Colab flow не зависел от устаревшего сигнатурного контракта.
- Notebook обязан оставаться совместимым с Google Colab T4 runtime: `SFTConfig` не должен полагаться на `max_seq_length` в устаревшей сигнатуре, а baseline mixed precision для воспроизводимого smoke-train — `fp16=False` и `bf16=False`, пока repo не зафиксирует отдельно проверенный AMP-контур.

## Локальные команды/проверки

```bash
python3 services/AgentService/training/prepare_dataset.py \
  --assistant-db qa-artifacts/sample-assistant.db \
  --snapshot-dir qa-artifacts/project-snapshots \
  --output-dir qa-artifacts/qwen-dataset

python3 services/AgentService/training/prepare_dataset.py \
  --assistant-db qa-artifacts/sample-assistant.db \
  --project-id 13b89775-9f16-4635-9487-bf0ed454c281 \
  --management-service-url http://api-management:5003 \
  --internal-service-name ai-service \
  --internal-management-key "$INTERNAL_AUTH_AI_TO_MANAGEMENT_KEY" \
  --output-dir qa-artifacts/qwen-dataset-live

python3 services/AgentService/training/package_colab_bundle.py \
  --dataset-dir qa-artifacts/qwen-dataset-live/out \
  --output-dir qa-artifacts/qwen-colab-bundles \
  --bundle-name collaby-qwen-colab-live

python3 services/AgentService/training/publish_adapter_package.py \
  --adapter-file qa-artifacts/final-adapter-f16.gguf \
  --version 2026-03-20-colab \
  --output-manifest qa-artifacts/final-adapter-manifest.json

python3 services/AgentService/training/export_adapter_manifest.py \
  --adapter-file qa-artifacts/final-adapter-f16.gguf \
  --version 2026-03-20-colab \
  --download-url https://github.com/MarkZamore/Collaby/releases/download/adapter-VERSION/adapter.gguf \
  --output qa-artifacts/final-adapter-manifest.json

python3 -m py_compile \
  services/AgentService/training/prepare_dataset.py \
  services/AgentService/training/package_colab_bundle.py \
  services/AgentService/training/publish_adapter_package.py \
  services/AgentService/training/export_adapter_manifest.py
```

## Связанные документы

- [../../../README.md](../../../README.md)
- [../README.md](../README.md)

Локальный runtime и запуск определены в [корневом README](../../../README.md).

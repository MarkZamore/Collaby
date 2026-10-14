# AgentService

## Назначение

`assistant` — внутренний Python AI runtime для mentor/assistant, скрытого контекста workspace и локального распознавания речи. Frontend обращается к нему через same-origin `/api/ai/*`.

## Что здесь является источником истины

- `main.py` — HTTP-контракт, JWT authorization, startup загрузки обязательной модели и health.
- `agent.py` — orchestration, SQLite, internal read-only queries, inference и ASR.
- `Dockerfile`, `requirements.txt`, `requirements-runtime.txt` — Python 3.12 CPU runtime с OpenBLAS; CUDA выключен. Для сборки нужны `pkg-config` и два параллельных процесса CMake.
- `bootstrap_runtime.py`, `fetch_model_assets.py`, `model-manifest.json`, `adapter-manifest.json`, `assets/` — materialization и проверка SHA-256/размеров.
- `tests/` — HTTP, retrieval, обязательность адаптера, event loop, восстановление загрузок и публикация Releases.
- `verify_native_runtime.py` — декодирование WAV настоящим faster-whisper/PyAV без скачивания моделей (`--decode-only`) либо полный smoke Qwen/LoRA и ASR из кэша.
- [training/README.md](training/README.md) — датасет, Colab и публикация будущих адаптеров.

## Что агент должен знать перед изменениями

- Kubernetes Service `assistant:8100` обслуживает `/api/ai/health`, `/runtime`, `/mentor/chat`, `/mentor/transcribe`, `/mentor/drafts`, `/mentor/context`, `/assistant/chat`, `/assistant/search`, `/knowledge/sync`; legacy `/chat` и `/followups` сохраняются.
- Образ задаёт `AI_REQUIRE_ADAPTER=true`. Перед HTTP startup загружаются Qwen2.5-1.5B-Instruct и встроенный GGUF LoRA. Нет повторной загрузки без `lora_path`; отсутствие/несовместимость адаптера приводит к ошибке startup, health возвращает 503 до его загрузки. Metadata включает `modelLoaded`, `adapterLoaded`, `adapterSha256`.
- `assets/final-adapter-f16-2026-03-20-colab-0642.gguf`: 36 956 896 байт, SHA-256 `4db31ce13521a516042626be61e52611dab6972467ebb958776894a2eff86d26`. Bootstrap читает его из образа по `file:///app/assets/...`; токен registry не нужен.
- `AI_STATE_DIR` находится на PVC: SQLite `assistant.db`, Qwen, adapter и ASR cache сохраняются. Повреждённый файл заменяется, загрузки пишутся в `.part`; незавершённый файл не используется. Три неуспешные попытки дают явную ошибку, повторный запуск восстанавливает кэш. Первый запуск скачивает Qwen и Whisper из интернета.
- ASR использует `faster-whisper`, `cpu/int8`; PyAV закреплён на 18.0.0, потому что PyAV 19 удалил `metadata_errors`, используемый faster-whisper 1.2.1. Блокирующие inference, summarization и ASR выполняются через `asyncio.to_thread`; обращения к каждой native модели сериализуются. Загруженное аудио удаляется в `finally`.
- Ответ `/assistant/chat` содержит `generationMode`: `local-model` либо явный `fallback-render`, если результат модели не прошёл проверку формата. Этот render не заменяет требование загрузить обязательный адаптер.
- Retrieval keyword-first, без отдельного embedding/vector runtime. Пустой `assignedTaskIds=[]` не раскрывает чужие задачи; `None` используется только в unrestricted internal сценариях. Assistant автоматически обновляет устаревший project snapshot.
- Internal queries читают `ManagementService` и `UserService` через host-prefixes `/api/management/internal/v1/queries/*`, `/api/users/internal/v1/queries/*`; AI не меняет domain data. Конфигурация URLs/внутренних ключей/JWT находится в Kubernetes ConfigMap/Secret.
- Demo seed должен создать hidden/task context через `/api/ai/mentor/context`; локальные ссылки используют `COLLABY_PUBLIC_BASE_URL`.
- OpenTelemetry pins должны оставаться в одной release-линейке. CPU runtime содержит `libgomp1`; импорт native библиотеки проверяется реальным smoke, unit tests не доказывают загрузку модели.

## Локальные команды/проверки

```powershell
python -m unittest discover -s services/AgentService/tests -p "test_*.py"
docker build -t collaby/assistant:verified services/AgentService
docker run --rm --entrypoint python collaby/assistant:verified verify_native_runtime.py --decode-only
```

Публикация нового адаптера выполняется вручную через `training/publish_release.py` с локальным `GITHUB_TOKEN`; runtime продолжает использовать bundled asset, пока tracked manifest/образ не изменены явно.

## Связанные документы

[Корневой README](../../README.md), [Kubernetes](../../kubernetes/README.md), [Training](training/README.md), [Frontend](../../frontend/README.md).

import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import AsyncMock, patch
from uuid import uuid4


SERVICE_DIR = Path(__file__).resolve().parents[1]
STATE_DIR = Path(tempfile.mkdtemp(prefix="collaby-agent-core-"))

os.environ.setdefault("AI_STATE_DIR", str(STATE_DIR))

if str(SERVICE_DIR) not in sys.path:
    sys.path.insert(0, str(SERVICE_DIR))

import agent  # noqa: E402


class FakeResponse:
    def __init__(self, *, status_code=200, payload=None):
        self.status_code = status_code
        self._payload = payload or {}

    def raise_for_status(self):
        if self.status_code >= 400:
            raise agent.httpx.HTTPStatusError(
                f"HTTP {self.status_code}",
                request=agent.httpx.Request("GET", "http://internal.test"),
                response=agent.httpx.Response(self.status_code),
            )

    def json(self):
        return self._payload


class FakeAsyncClient:
    def __init__(self, *, get_response=None, post_response=None):
        self.get_response = get_response or FakeResponse(payload={})
        self.post_response = post_response or FakeResponse(payload=[])
        self.get_calls = []
        self.post_calls = []

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc, tb):
        return False

    async def get(self, url, headers=None):
        self.get_calls.append({"url": url, "headers": headers or {}})
        return self.get_response

    async def post(self, url, headers=None, json=None):
        self.post_calls.append({"url": url, "headers": headers or {}, "json": json})
        return self.post_response


class FakeChatModel:
    def __init__(self, content: str):
        self.content = content

    def create_chat_completion(self, **kwargs):
        _ = kwargs
        return {
            "choices": [
                {
                    "message": {
                        "content": self.content,
                    }
                }
            ]
        }


class AgentCoreTests(unittest.IsolatedAsyncioTestCase):
    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(STATE_DIR, ignore_errors=True)

    async def test_generate_followups_returns_guidance(self):
        service = agent.AgentService()

        result = await service.generate_follow_ups("Контекст по проекту обновлен")

        self.assertTrue(result)
        self.assertIn("Синхронизировать данные задач из ManagementService", result)

    async def test_legacy_chat_returns_orchestration_message(self):
        service = agent.AgentService()
        principal = agent.AuthenticatedPrincipal(
            user_id=str(uuid4()),
            email="tester@example.com",
            username="tester",
            is_system_admin=False,
        )

        result = await service.legacy_chat(
            principal=principal,
            message="Помоги разобрать доску",
            history=[{"role": "user", "content": "Нужно навести порядок"}],
            context={"projectId": "project-1", "tasks": [{"id": "task-1"}], "columns": [{"id": "todo"}]},
        )

        self.assertIsNone(result["action"])
        self.assertEqual(result["actions"], [])
        self.assertTrue(result["followUps"])
        self.assertIn("orchestration layer", result["reply"])

    def test_backend_metadata_reports_local_runtime(self):
        root = STATE_DIR / f"metadata-baseline-{uuid4()}"
        root.mkdir(parents=True, exist_ok=True)
        try:
            config = agent.AgentConfig(
                state_dir=root,
                database_path=root / "assistant.db",
                model_dir=root / "models",
                adapters_dir=root / "adapters",
                model_manifest_path=root / "missing-model-manifest.json",
                adapter_manifest_path=root / "missing-adapter-manifest.json",
                model_file_name="model.gguf",
                asr_model_size="small",
                llm_context_tokens=2048,
                llm_gpu_layers=-1,
                asr_device="cpu",
                asr_compute_type="int8",
                public_base_url="https://collaby.org",
                management_service_url="http://api-management:5003",
                user_service_url="http://api-users:5002",
                internal_service_name="ai-service",
                internal_management_key="management-secret",
                internal_user_key="user-secret",
                jwt_signing_key="signing-key",
                jwt_issuer="issuer",
                jwt_audience="audience",
                model_name="Qwen2.5-1.5B-Instruct",
                inference_backend="local-qwen-gguf",
                external_search_enabled=True,
            )
            service = agent.AgentService(config)
            metadata = service.get_backend_metadata()

            self.assertEqual(metadata["backend"], "local-orchestrator")
            self.assertEqual(metadata["state"], "ready")
            self.assertFalse(metadata["actionsEnabled"])
            self.assertEqual(metadata["modelName"], "Qwen2.5-1.5B-Instruct")
            self.assertEqual(metadata["inferenceBackend"], service.config.inference_backend)
            self.assertEqual(metadata["stateDir"], str(service.config.state_dir))
            self.assertEqual(metadata["modelDir"], str(service.config.model_dir))
            self.assertEqual(
                metadata["modelFile"],
                str(service.config.model_dir / service.config.model_file_name),
            )
            self.assertEqual(metadata["modelManifestPath"], str(service.config.model_manifest_path))
            self.assertEqual(metadata["adapterManifestPath"], str(service.config.adapter_manifest_path))
            self.assertIsNone(metadata["adapterFormat"])
            self.assertFalse(metadata["adapterRuntimeCompatible"])
            self.assertFalse(metadata["modelReady"])
            self.assertTrue(metadata["supportsVoiceUpload"])
            self.assertEqual(metadata["supportsExternalSearch"], service.config.external_search_enabled)
            self.assertEqual(metadata["trainingStrategy"], "SFT + QLoRA (4-bit)")
        finally:
            shutil.rmtree(root, ignore_errors=True)

    def test_resolve_adapter_path_ignores_non_gguf_manifest(self):
        root = STATE_DIR / f"adapter-incompatible-{uuid4()}"
        root.mkdir(parents=True, exist_ok=True)
        try:
            adapter_manifest = root / "adapter-manifest.json"
            adapter_manifest.write_text(
                '{"adapter":{"format":"peft-directory","filename":"adapter.zip"}}',
                encoding="utf-8",
            )
            config = agent.AgentConfig(
                state_dir=root,
                database_path=root / "assistant.db",
                model_dir=root / "models",
                adapters_dir=root / "adapters",
                model_manifest_path=root / "model-manifest.json",
                adapter_manifest_path=adapter_manifest,
                model_file_name="model.gguf",
                asr_model_size="small",
                llm_context_tokens=2048,
                llm_gpu_layers=-1,
                asr_device="cpu",
                asr_compute_type="int8",
                public_base_url="https://collaby.org",
                management_service_url="http://api-management:5003",
                user_service_url="http://api-users:5002",
                internal_service_name="ai-service",
                internal_management_key="management-secret",
                internal_user_key="user-secret",
                jwt_signing_key="signing-key",
                jwt_issuer="issuer",
                jwt_audience="audience",
                model_name="Qwen2.5-1.5B-Instruct",
                inference_backend="local-qwen-gguf",
                external_search_enabled=True,
            )
            service = agent.AgentService(config)
            metadata = service.get_backend_metadata()

            self.assertIsNone(service._resolve_adapter_path())
            self.assertEqual(metadata["adapterFormat"], "peft-directory")
            self.assertFalse(metadata["adapterRuntimeCompatible"])
            self.assertFalse(metadata["adapterReady"])
        finally:
            shutil.rmtree(root, ignore_errors=True)

    def test_backend_metadata_reports_gguf_adapter_contract_when_manifest_present(self):
        root = STATE_DIR / f"adapter-compatible-{uuid4()}"
        root.mkdir(parents=True, exist_ok=True)
        try:
            adapters_dir = root / "adapters"
            adapters_dir.mkdir(parents=True, exist_ok=True)
            (adapters_dir / "adapter.gguf").write_bytes(b"adapter")
            adapter_manifest = root / "adapter-manifest.json"
            adapter_manifest.write_text(
                '{"adapter":{"format":"gguf-lora","filename":"adapter.gguf"}}',
                encoding="utf-8",
            )
            config = agent.AgentConfig(
                state_dir=root,
                database_path=root / "assistant.db",
                model_dir=root / "models",
                adapters_dir=adapters_dir,
                model_manifest_path=root / "model-manifest.json",
                adapter_manifest_path=adapter_manifest,
                model_file_name="model.gguf",
                asr_model_size="small",
                llm_context_tokens=2048,
                llm_gpu_layers=-1,
                asr_device="cpu",
                asr_compute_type="int8",
                public_base_url="https://collaby.org",
                management_service_url="http://api-management:5003",
                user_service_url="http://api-users:5002",
                internal_service_name="ai-service",
                internal_management_key="management-secret",
                internal_user_key="user-secret",
                jwt_signing_key="signing-key",
                jwt_issuer="issuer",
                jwt_audience="audience",
                model_name="Qwen2.5-1.5B-Instruct",
                inference_backend="local-qwen-gguf",
                external_search_enabled=True,
            )
            service = agent.AgentService(config)
            metadata = service.get_backend_metadata()

            self.assertEqual(metadata["adapterFormat"], "gguf-lora")
            self.assertTrue(metadata["adapterRuntimeCompatible"])
            self.assertTrue(metadata["adapterReady"])
            self.assertEqual(service._resolve_adapter_path(), adapters_dir / "adapter.gguf")
        finally:
            shutil.rmtree(root, ignore_errors=True)

    async def test_fetch_project_snapshot_uses_internal_service_key_header(self):
        config = agent.AgentConfig(
            state_dir=STATE_DIR,
            database_path=STATE_DIR / "assistant.db",
            model_dir=STATE_DIR / "models",
            adapters_dir=STATE_DIR / "adapters",
            model_manifest_path=STATE_DIR / "model-manifest.json",
            adapter_manifest_path=STATE_DIR / "adapter-manifest.json",
            model_file_name="model.gguf",
            asr_model_size="small",
            llm_context_tokens=2048,
            llm_gpu_layers=-1,
            asr_device="cpu",
            asr_compute_type="int8",
            public_base_url="https://collaby.org",
            management_service_url="http://api-management:5003",
            user_service_url="http://api-users:5002",
            internal_service_name="ai-service",
            internal_management_key="management-secret",
            internal_user_key="user-secret",
            jwt_signing_key="signing-key",
            jwt_issuer="issuer",
            jwt_audience="audience",
            model_name="Qwen2.5-1.5B-Instruct",
            inference_backend="local-qwen-gguf",
            external_search_enabled=True,
        )
        client = FakeAsyncClient(get_response=FakeResponse(payload={"project": {"id": "project-1"}}))
        with patch.object(agent.httpx, "AsyncClient", return_value=client):
            result = await agent.InternalServiceClients(config).fetch_project_snapshot("project-1")

        self.assertEqual(result, {"project": {"id": "project-1"}})
        self.assertEqual(len(client.get_calls), 1)
        self.assertEqual(
            client.get_calls[0]["url"],
            "http://api-management:5003/api/management/internal/v1/queries/projects/project-1/assistant-snapshot",
        )
        headers = client.get_calls[0]["headers"]
        self.assertEqual(headers[agent.INTERNAL_CALLER_HEADER], "ai-service")
        self.assertEqual(headers[agent.INTERNAL_CREDENTIAL_HEADER], "management-secret")
        self.assertNotIn("X-Internal-Api-Key", headers)

    async def test_fetch_user_profiles_uses_internal_service_key_header(self):
        config = agent.AgentConfig(
            state_dir=STATE_DIR,
            database_path=STATE_DIR / "assistant.db",
            model_dir=STATE_DIR / "models",
            adapters_dir=STATE_DIR / "adapters",
            model_manifest_path=STATE_DIR / "model-manifest.json",
            adapter_manifest_path=STATE_DIR / "adapter-manifest.json",
            model_file_name="model.gguf",
            asr_model_size="small",
            llm_context_tokens=2048,
            llm_gpu_layers=-1,
            asr_device="cpu",
            asr_compute_type="int8",
            public_base_url="https://collaby.org",
            management_service_url="http://api-management:5003",
            user_service_url="http://api-users:5002",
            internal_service_name="ai-service",
            internal_management_key="management-secret",
            internal_user_key="user-secret",
            jwt_signing_key="signing-key",
            jwt_issuer="issuer",
            jwt_audience="audience",
            model_name="Qwen2.5-1.5B-Instruct",
            inference_backend="local-qwen-gguf",
            external_search_enabled=True,
        )
        client = FakeAsyncClient(post_response=FakeResponse(payload=[{"userId": "user-1", "displayName": "User"}]))
        with patch.object(agent.httpx, "AsyncClient", return_value=client):
            result = await agent.InternalServiceClients(config).fetch_user_profiles(["user-1"])

        self.assertEqual(result, {"user-1": {"userId": "user-1", "displayName": "User"}})
        self.assertEqual(len(client.post_calls), 1)
        self.assertEqual(
            client.post_calls[0]["url"],
            "http://api-users:5002/api/users/internal/v1/queries/users/batch",
        )
        headers = client.post_calls[0]["headers"]
        self.assertEqual(headers[agent.INTERNAL_CALLER_HEADER], "ai-service")
        self.assertEqual(headers[agent.INTERNAL_CREDENTIAL_HEADER], "user-secret")
        self.assertNotIn("X-Internal-Api-Key", headers)

    def test_generate_with_local_model_preserves_markdown_sections(self):
        service = agent.AgentService()
        model = FakeChatModel(
            "## Краткий ответ\nКороткая суть.\n\n## Что делать дальше\n- Первый шаг\n- Второй шаг\n\n## Источники\n- internal"
        )

        with patch.object(service, "_get_local_model", return_value=model):
            content = service._generate_with_local_model(
                system_prompt="system",
                user_prompt="user",
            )

        self.assertIsInstance(content, str)
        self.assertIn("\n\n## Что делать дальше", content)
        self.assertIn("- Первый шаг", content)

    async def test_assistant_chat_refreshes_project_knowledge_when_assigned_task_context_is_stale(self):
        service = agent.AgentService()
        principal = agent.AuthenticatedPrincipal(
            user_id=str(uuid4()),
            email="tester@example.com",
            username="tester",
            is_system_admin=False,
        )
        stale_sources = [
            {
                "kind": "approved-summary",
                "title": "Скрытая выжимка",
                "taskId": None,
                "visibility": "hidden",
                "excerpt": "Сначала проверить deploy-launch.",
                "urlOrRef": "seed:hidden-summary",
            }
        ]
        refreshed_sources = [
            {
                "kind": "task",
                "title": "Задача: Проверить public assistant assigned context",
                "taskId": "task-1",
                "visibility": "task-context",
                "excerpt": "Title: Проверить public assistant assigned context",
                "urlOrRef": "task:task-1",
            }
        ]

        with (
            patch.object(service.storage, "search_internal", side_effect=[stale_sources, refreshed_sources]) as search_mock,
            patch.object(service, "sync_knowledge", new=AsyncMock(return_value={"savedItems": 2})) as sync_mock,
            patch.object(service, "_generate_assistant_answer", return_value=("answer", "local-model")),
        ):
            result = await service.assistant_chat(
                principal=principal,
                workspace_id="workspace-1",
                project_id="project-1",
                question="Что по назначенной задаче task-1?",
                assigned_task_ids=["task-1"],
                include_external_sources=False,
                history=[],
                session_id=None,
            )

        self.assertEqual(search_mock.call_count, 2)
        sync_mock.assert_awaited_once_with(
            principal=principal,
            workspace_id="workspace-1",
            project_id="project-1",
        )
        self.assertEqual(result["sources"], refreshed_sources)
        self.assertEqual(result["generationMode"], "local-model")

    async def test_assistant_search_skips_refresh_when_task_context_is_already_present(self):
        service = agent.AgentService()
        principal = agent.AuthenticatedPrincipal(
            user_id=str(uuid4()),
            email="tester@example.com",
            username="tester",
            is_system_admin=False,
        )
        sources = [
            {
                "kind": "comment",
                "title": "Комментарий к задаче",
                "taskId": "task-1",
                "visibility": "task-context",
                "excerpt": "Комментарий по назначенной задаче",
                "urlOrRef": "comment:task-1",
            }
        ]

        with (
            patch.object(service.storage, "search_internal", return_value=sources) as search_mock,
            patch.object(service, "sync_knowledge", new=AsyncMock()) as sync_mock,
        ):
            result = await service.assistant_search(
                principal=principal,
                workspace_id="workspace-1",
                project_id="project-1",
                query="Что по task-1?",
                assigned_task_ids=["task-1"],
                include_external_sources=False,
            )

        search_mock.assert_called_once()
        sync_mock.assert_not_awaited()
        self.assertEqual(result["items"], sources)
        self.assertFalse(result["knowledgeAutoRefreshed"])

    async def test_assistant_chat_refreshes_when_internal_source_urls_are_stale(self):
        service = agent.AgentService()
        principal = agent.AuthenticatedPrincipal(
            user_id=str(uuid4()),
            email="tester@example.com",
            username="tester",
            is_system_admin=False,
        )
        stale_sources = [
            {
                "kind": "project",
                "title": "Проект: qa-live",
                "taskId": None,
                "visibility": "hidden",
                "excerpt": "Project summary",
                "urlOrRef": "https://collaby.org/assistant",
            },
            {
                "kind": "task",
                "title": "Задача: qa-live task",
                "taskId": "task-1",
                "visibility": "task-context",
                "excerpt": "Title: qa-live task\nStatus: In Progress",
                "urlOrRef": "https://collaby.org/assistant",
            },
        ]
        refreshed_sources = [
            {
                "kind": "project",
                "title": "Проект: qa-live",
                "taskId": None,
                "visibility": "hidden",
                "excerpt": "Project summary",
                "urlOrRef": "https://collaby.org/workspace/qa-live/project/qa-live",
            },
            {
                "kind": "task",
                "title": "Задача: qa-live task",
                "taskId": "task-1",
                "visibility": "task-context",
                "excerpt": "Title: qa-live task\nStatus: In Progress",
                "urlOrRef": "https://collaby.org/workspace/qa-live/project/qa-live?taskId=task-1",
            },
        ]

        with (
            patch.object(service.storage, "search_internal", side_effect=[stale_sources, refreshed_sources]) as search_mock,
            patch.object(service, "sync_knowledge", new=AsyncMock(return_value={"savedItems": 2})) as sync_mock,
            patch.object(service, "_generate_assistant_answer", return_value=("answer", "local-model")),
        ):
            result = await service.assistant_chat(
                principal=principal,
                workspace_id="workspace-1",
                project_id="project-1",
                question="Сделай краткую сводку по моим задачам",
                assigned_task_ids=["task-1"],
                include_external_sources=False,
                history=[],
                session_id=None,
            )

        self.assertEqual(search_mock.call_count, 2)
        sync_mock.assert_awaited_once_with(
            principal=principal,
            workspace_id="workspace-1",
            project_id="project-1",
        )
        self.assertEqual(result["sources"], refreshed_sources)
        self.assertEqual(result["generationMode"], "local-model")

    async def test_assistant_chat_uses_default_project_and_author_context_when_query_tokens_miss(self):
        service = agent.AgentService()
        principal = agent.AuthenticatedPrincipal(
            user_id="owner-1",
            email="owner@example.com",
            username="owner",
            is_system_admin=False,
        )
        project_item = {
            "id": "knowledge-project",
            "origin_key": "project:project-1",
            "workspace_id": "workspace-1",
            "project_id": "project-1",
            "task_id": None,
            "kind": "project",
            "title": "Проект: qa-live",
            "content": "Project summary with current priorities.",
            "visibility": "hidden",
            "status": "approved",
            "source_ref": "project:project-1",
            "source_url": "https://collaby.org/assistant",
            "author_user_id": "owner-1",
            "origin": "sync",
            "updated_at": "2026-03-22T00:00:00+00:00",
        }
        authored_task = {
            "id": "knowledge-task",
            "origin_key": "task:task-1",
            "workspace_id": "workspace-1",
            "project_id": "project-1",
            "task_id": "task-1",
            "kind": "task",
            "title": "Задача: qa-live task",
            "content": "Title: qa-live task\nStatus: In Progress",
            "visibility": "task-context",
            "status": "approved",
            "source_ref": "task:task-1",
            "source_url": "https://collaby.org/assistant",
            "author_user_id": "owner-1",
            "origin": "sync",
            "updated_at": "2026-03-22T00:01:00+00:00",
        }

        with (
            patch.object(service.storage, "search_internal", side_effect=[[], []]) as search_mock,
            patch.object(service.storage, "list_context_items", return_value=[project_item, authored_task]),
            patch.object(service, "sync_knowledge", new=AsyncMock(return_value={"savedItems": 2})) as sync_mock,
            patch.object(service, "_generate_assistant_answer", return_value=("answer", "local-model")),
        ):
            result = await service.assistant_chat(
                principal=principal,
                workspace_id="workspace-1",
                project_id="project-1",
                question="Summarize my current tasks",
                assigned_task_ids=[],
                include_external_sources=False,
                history=[],
                session_id=None,
            )

        self.assertEqual(search_mock.call_count, 2)
        sync_mock.assert_awaited_once_with(
            principal=principal,
            workspace_id="workspace-1",
            project_id="project-1",
        )
        self.assertEqual([source["kind"] for source in result["sources"]], ["project", "task"])
        self.assertEqual(result["generationMode"], "local-model")

    def test_usable_assistant_answer_rejects_generic_model_failure(self):
        service = agent.AgentService()
        answer = "\n".join(
            [
                "## Краткий ответ",
                "Вопрос участника не был полностью прочитан и не содержит достаточных деталей для ответа.",
                "",
                "## Что делать дальше",
                "- Попросить больше информации.",
                "",
                "## Связанные задачи",
                "- Test task (`task-1`)",
                "",
                "## Источники",
                "- internal: Test task -> task:1",
            ]
        )

        self.assertFalse(
            service._is_usable_assistant_answer(
                answer,
                internal_sources=[{"taskId": "task-1", "title": "Test task"}],
            )
        )

    def test_extract_section_text_returns_only_requested_section(self):
        service = agent.AgentService()
        markdown = "\n".join(
            [
                "## Краткий ответ",
                "Короткая суть.",
                "",
                "## Что делать дальше",
                "- Первый шаг",
                "- Второй шаг",
                "",
                "## Источники",
                "- internal",
            ]
        )

        self.assertEqual(service._extract_section_text(markdown, "## Краткий ответ"), "Короткая суть.")
        self.assertEqual(
            service._extract_bullets(markdown, "## Что делать дальше"),
            ["Первый шаг", "Второй шаг"],
        )

    def test_extract_first_section_text_returns_first_markdown_section(self):
        service = agent.AgentService()
        markdown = "\n".join(
            [
                "## Краткий ответ",
                "Первая строка.",
                "Вторая строка.",
                "",
                "## Что делать дальше",
                "- Шаг 1",
            ]
        )

        self.assertEqual(
            service._extract_first_section_text(markdown),
            "Первая строка.\nВторая строка.",
        )

    def test_render_assistant_answer_prefers_task_context_and_deduplicates_tasks(self):
        service = agent.AgentService()
        task_id = "task-1"
        answer = service._render_assistant_answer(
            "Что делать по самой срочной задаче?",
            [
                {
                    "kind": "project",
                    "title": "Проект: Demo",
                    "taskId": None,
                    "excerpt": "Workspace: Demo\nProject: Demo\nTask count: 24",
                    "urlOrRef": "project:demo",
                },
                {
                    "kind": "comment",
                    "title": "Комментарий к задаче: Починить deploy-launch",
                    "taskId": task_id,
                    "excerpt": "Task: Починить deploy-launch\nComment: Нужно проверить cutover",
                    "urlOrRef": "comment:1",
                },
                {
                    "kind": "task",
                    "title": "Задача: Починить deploy-launch",
                    "taskId": task_id,
                    "excerpt": "\n".join(
                        [
                            "Title: Починить deploy-launch",
                            "Status: In Progress",
                            "Priority: 1",
                            "DueDate: 2026-03-21",
                        ]
                    ),
                    "urlOrRef": "task:1",
                },
            ],
            [],
        )

        self.assertIn("## Краткий ответ", answer)
        self.assertIn("Починить deploy-launch", answer)
        self.assertIn("Статус: In Progress.", answer)
        self.assertIn("Приоритет: 1.", answer)
        self.assertIn("Дедлайн: 2026-03-21.", answer)
        self.assertEqual(answer.count("`task-1`"), 1)
        self.assertIn("Внутренний источник:", answer)

    def test_search_internal_prioritizes_urgent_open_tasks_for_urgency_questions(self):
        service = agent.AgentService()
        workspace_id = str(uuid4())

        service.storage.upsert_knowledge_item(
            origin_key=f"task:{uuid4()}",
            workspace_id=workspace_id,
            project_id=None,
            task_id="task-low",
            kind="task",
            title="Задача: Add Prometheus metrics",
            content="\n".join(
                [
                    "Title: Add Prometheus metrics",
                    "Status: В процессе",
                    "Priority: Low",
                    "DueDate: 2026-04-08T10:29:54Z",
                ]
            ),
            visibility="task-context",
            status="approved",
            source_ref="task:task-low",
            source_url="https://collaby.org/assistant",
            author_user_id="tester",
            origin="sync",
        )
        service.storage.upsert_knowledge_item(
            origin_key=f"task:{uuid4()}",
            workspace_id=workspace_id,
            project_id=None,
            task_id="task-critical",
            kind="task",
            title="Задача: Fix memory leak in WebSocket Hub",
            content="\n".join(
                [
                    "Title: Fix memory leak in WebSocket Hub",
                    "Status: В процессе",
                    "Priority: critical",
                    "DueDate: 2026-03-20T08:00:00Z",
                ]
            ),
            visibility="task-context",
            status="approved",
            source_ref="task:task-critical",
            source_url="https://collaby.org/assistant",
            author_user_id="tester",
            origin="sync",
        )
        service.storage.upsert_knowledge_item(
            origin_key=f"task:{uuid4()}",
            workspace_id=workspace_id,
            project_id=None,
            task_id="task-done",
            kind="task",
            title="Задача: Analytics drill",
            content="\n".join(
                [
                    "Title: Analytics drill",
                    "Status: Готово",
                    "Priority: High",
                    "DueDate: 2026-03-21T08:00:00Z",
                ]
            ),
            visibility="task-context",
            status="approved",
            source_ref="task:task-done",
            source_url="https://collaby.org/assistant",
            author_user_id="tester",
            origin="sync",
        )

        results = service.storage.search_internal(
            workspace_id=workspace_id,
            query="Какие задачи самые срочные и что делать дальше?",
            project_id=None,
            allowed_task_ids=None,
            include_hidden=True,
            limit=3,
        )

        self.assertEqual(results[0]["taskId"], "task-critical")

    def test_search_internal_blocks_task_context_when_allowed_task_ids_is_empty_list(self):
        service = agent.AgentService()
        workspace_id = str(uuid4())
        project_id = str(uuid4())

        service.storage.upsert_knowledge_item(
            origin_key=f"project:{project_id}",
            workspace_id=workspace_id,
            project_id=project_id,
            task_id=None,
            kind="project",
            title="Проект: qa-live",
            content="Title: qa-live task overview",
            visibility="hidden",
            status="approved",
            source_ref=f"project:{project_id}",
            source_url="https://collaby.org/assistant",
            author_user_id="owner-1",
            origin="sync",
        )
        service.storage.upsert_knowledge_item(
            origin_key="task:task-sensitive",
            workspace_id=workspace_id,
            project_id=project_id,
            task_id="task-sensitive",
            kind="task",
            title="Задача: qa-live task overview",
            content="Title: qa-live task overview\nStatus: In Progress",
            visibility="task-context",
            status="approved",
            source_ref="task:task-sensitive",
            source_url="https://collaby.org/assistant",
            author_user_id="owner-1",
            origin="sync",
        )

        results = service.storage.search_internal(
            workspace_id=workspace_id,
            query="qa-live task overview",
            project_id=project_id,
            allowed_task_ids=[],
            include_hidden=True,
            limit=5,
        )

        self.assertEqual([item["kind"] for item in results], ["project"])

    def test_render_assistant_answer_external_only_uses_canonical_links(self):
        service = agent.AgentService()

        answer = service._render_assistant_answer(
            "Что проверить перед релизом?",
            [],
            [
                {
                    "kind": "external",
                    "title": "GitHub CI guide",
                    "urlOrRef": "//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fgithub-actions",
                }
            ],
        )

        self.assertIn("GitHub CI guide", answer)
        self.assertIn("https://example.com/github-actions", answer)
        self.assertIn("Для этого запроса не найдено внутренних task-context задач", answer)
        self.assertNotIn("duckduckgo.com/l/?uddg=", answer)

    def test_generate_assistant_answer_external_only_skips_local_model(self):
        service = agent.AgentService()
        external_sources = [
            {
                "kind": "external",
                "title": "Release checklist",
                "urlOrRef": "https://example.com/release-checklist",
            }
        ]

        with patch.object(service, "_generate_with_local_model", side_effect=AssertionError("llm should be skipped")):
            answer, generation_mode = service._generate_assistant_answer(
                "Что проверить перед GitHub CI/CD релизом?",
                [],
                external_sources,
            )

        self.assertEqual(generation_mode, "fallback-render")
        self.assertIn("Release checklist", answer)
        self.assertIn("rollback plan", answer)
        self.assertIn("https://example.com/release-checklist", answer)

    def test_build_assistant_prompt_avoids_raw_source_separators(self):
        service = agent.AgentService()

        prompt = service._build_assistant_prompt(
            "Какие риски есть у релиза?",
            [
                {
                    "kind": "task",
                    "title": "Release readiness review",
                    "excerpt": "Task: review deploy-launch blockers and rollout notes",
                }
            ],
            [
                {
                    "kind": "external",
                    "title": "GitHub release guide",
                    "urlOrRef": "https://example.com/releases",
                }
            ],
        )

        self.assertIn("Внутренний источник:", prompt)
        self.assertIn("Внешний источник:", prompt)
        self.assertNotIn("internal |", prompt)
        self.assertNotIn("external |", prompt)

    def test_render_assistant_answer_uses_approved_summary_context_when_no_tasks(self):
        service = agent.AgentService()

        answer = service._render_assistant_answer(
            "deploy-launch pipeline release metrics",
            [
                {
                    "kind": "approved-summary",
                    "title": "Публичный release focus: deploy-launch",
                    "taskId": None,
                    "excerpt": (
                        "Для публичного acceptance ключевой сигнал готовности релиза — успешный deploy-launch в GitHub pipeline."
                    ),
                    "urlOrRef": "seed:public-release-focus",
                },
                {
                    "kind": "approved-summary",
                    "title": "Публичный CI/CD checklist",
                    "taskId": None,
                    "excerpt": "Перед production release нужно проверить build, dotnet, test, deploy-prepare и deploy-launch.",
                    "urlOrRef": "seed:public-ci-checklist",
                },
            ],
            [],
        )

        self.assertIn("Публичный release focus: deploy-launch", answer)
        self.assertIn("Публичный CI/CD checklist", answer)
        self.assertIn("ключевой сигнал готовности релиза", answer)
        self.assertNotIn("Подходящий внутренний контекст не найден", answer)
        self.assertIn("ответ уровня конкретной задачи", answer)

    def test_generate_assistant_answer_reports_local_model_mode(self):
        service = agent.AgentService()
        local_answer = "\n".join(
            [
                "## Краткий ответ",
                "Сначала стабилизируйте deploy-launch и проверьте связанный task-context.",
                "",
                "## Что делать дальше",
                "- Проверить последний deploy-launch job.",
                "- Сверить hidden context по релизу.",
                "",
                "## Связанные задачи",
                "- Fix memory leak in WebSocket Hub (`task-1`)",
                "",
                "## Источники",
                "- internal: Релизный фокус -> task:task-1",
            ]
        )

        with patch.object(service, "_generate_with_local_model", return_value=local_answer):
            answer, generation_mode = service._generate_assistant_answer(
                "Что делать по deploy-launch?",
                [
                    {
                        "kind": "task",
                        "title": "Fix memory leak in WebSocket Hub",
                        "taskId": "task-1",
                        "excerpt": "Task: Fix memory leak in WebSocket Hub\nStatus: In Progress",
                        "urlOrRef": "task:task-1",
                    }
                ],
                [],
            )

        self.assertEqual(generation_mode, "local-model")
        self.assertIn("## Краткий ответ", answer)
        self.assertIn("deploy-launch", answer)
        self.assertIn("Внутренний источник:", answer)

    def test_generate_assistant_answer_normalizes_unstructured_local_model_reply(self):
        service = agent.AgentService()
        unstructured_local_answer = "\n".join(
            [
                "Ваш запрос содержит информацию о задачах и метриках для релиза WebSocket Hub и CI/CD.",
                "1. Цель задачи:",
                "- Устранение утечки памяти в WebSocket Hub.",
                "- Проверка переменных GitHub.",
                "- Подготовка production pipeline.",
            ]
        )

        with patch.object(service, "_generate_with_local_model", return_value=unstructured_local_answer):
            answer, generation_mode = service._generate_assistant_answer(
                "Что делать по deploy-launch?",
                [
                    {
                        "kind": "task",
                        "title": "Fix memory leak in WebSocket Hub",
                        "taskId": "task-1",
                        "excerpt": "Task: Fix memory leak in WebSocket Hub\nStatus: In Progress",
                        "urlOrRef": "task:task-1",
                    },
                    {
                        "kind": "approved-summary",
                        "title": "Релизный фокус: WebSocket Hub и CI/CD",
                        "taskId": None,
                        "excerpt": "Deploy-launch подтверждает готовность релиза после health-check.",
                        "urlOrRef": "seed:release-focus",
                    },
                ],
                [],
            )

        self.assertEqual(generation_mode, "local-model")
        self.assertEqual(answer.count("## "), 4)
        self.assertIn("WebSocket Hub", answer)
        self.assertIn("Подготовка production pipeline", answer)

    def test_normalize_local_model_assistant_answer_drops_source_dump_summary_and_localizes_priority(self):
        service = agent.AgentService()

        normalized = service._normalize_local_model_assistant_answer(
            question="Сделай краткую сводку по моим задачам",
            local_reply="\n".join(
                [
                    "## Краткий ответ",
                    "Внутренний источник содержит информацию о задаче и показывает приоритет Мedium.",
                    "",
                    "## Что делать дальше",
                    "- Проверь задачу.",
                    "",
                    "## Связанные задачи",
                    "- Demo task (`task-1`)",
                    "",
                    "## Источники",
                    "- Внутренний источник: Demo -> task:task-1",
                ]
            ),
            internal_sources=[
                {
                    "kind": "task",
                    "title": "Задача: Demo task",
                    "taskId": "task-1",
                    "excerpt": "Title: Demo task\nStatus: In Progress\nPriority: Medium",
                    "urlOrRef": "https://collaby.org/workspace/demo/project/project-1?taskId=task-1",
                }
            ],
            external_sources=[],
        )

        self.assertNotIn("Внутренний источник содержит информацию", normalized)
        self.assertNotIn("Мedium", normalized)
        self.assertIn("Средний", normalized)


if __name__ == "__main__":
    unittest.main()

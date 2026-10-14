import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from uuid import uuid4

import jwt
from fastapi.testclient import TestClient


SERVICE_DIR = Path(__file__).resolve().parents[1]
STATE_DIR = Path(tempfile.mkdtemp(prefix="collaby-ai-tests-"))

os.environ.setdefault("AI_STATE_DIR", str(STATE_DIR))
os.environ.setdefault("IDENTITY_JWT_KEY", "test-signing-key-for-collaby-ai-suite-32bytes")
os.environ.setdefault("IDENTITY_JWT_ISSUER", "collaby-identity")
os.environ.setdefault("IDENTITY_JWT_AUDIENCE", "collaby-clients")
os.environ.setdefault("AI_EXTERNAL_SEARCH_ENABLED", "false")

if str(SERVICE_DIR) not in sys.path:
    sys.path.insert(0, str(SERVICE_DIR))

import main  # noqa: E402


def build_token(user_id: str | None = None) -> str:
    principal_id = user_id or str(uuid4())
    return jwt.encode(
        {
            "sub": principal_id,
            "email": "tester@example.com",
            "unique_name": "tester",
            "iss": os.environ["IDENTITY_JWT_ISSUER"],
            "aud": os.environ["IDENTITY_JWT_AUDIENCE"],
        },
        os.environ["IDENTITY_JWT_KEY"],
        algorithm="HS256",
    )


class AgentApiContractTests(unittest.TestCase):
    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(STATE_DIR, ignore_errors=True)

    def setUp(self):
        self.client = TestClient(main.app)
        self.token = build_token()
        self.headers = {"Authorization": f"Bearer {self.token}"}

    def test_swagger_ui_is_served_under_internal_swagger_prefix(self):
        response = self.client.get("/swagger")

        self.assertEqual(response.status_code, 200)
        self.assertIn("Swagger UI", response.text)

    def test_openapi_json_contains_new_ai_surface(self):
        response = self.client.get("/swagger/v1/swagger.json")

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["info"]["title"], "Collaby AI Agent")
        self.assertIn("/api/ai/assistant/chat", payload["paths"])
        self.assertIn("/api/ai/mentor/context", payload["paths"])
        self.assertIn("/api/ai/health", payload["paths"])

    def test_health_reports_local_orchestrator_backend(self):
        response = self.client.get("/api/ai/health")

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["status"], "ok")
        self.assertEqual(payload["backend"], "local-orchestrator")
        self.assertEqual(payload["state"], "ready")
        self.assertTrue(payload["supportsVoiceUpload"])
        self.assertEqual(payload["toolSchemaCount"], len(main.get_tool_schemas()))

    def test_runtime_requires_authentication(self):
        response = self.client.get("/api/ai/runtime")

        self.assertEqual(response.status_code, 401)

    def test_runtime_reports_stateful_configuration(self):
        response = self.client.get("/api/ai/runtime", headers=self.headers)

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["modelName"], "Qwen2.5-1.5B-Instruct")
        self.assertEqual(payload["inferenceBackend"], "local-qwen-gguf")
        self.assertIn("databasePath", payload)

    def test_mentor_context_roundtrip_returns_confirmation(self):
        workspace_id = str(uuid4())
        project_id = str(uuid4())
        source_ref = f"manual:{workspace_id}:summary"

        create_response = self.client.post(
            "/api/ai/mentor/context",
            headers=self.headers,
            json={
                "workspaceId": workspace_id,
                "projectId": project_id,
                "title": "Summary after sync",
                "content": "Confirmed scope, backlog and next milestone.",
                "kind": "approved-summary",
                "status": "approved",
                "visibility": "hidden",
                "sourceRef": source_ref,
            },
        )

        self.assertEqual(create_response.status_code, 200)
        self.assertEqual(create_response.json()["message"], "Контекст обновлен")

        list_response = self.client.get(
            "/api/ai/mentor/context",
            headers=self.headers,
            params={"workspaceId": workspace_id, "projectId": project_id, "status": "approved"},
        )

        self.assertEqual(list_response.status_code, 200)
        items = list_response.json()["items"]
        self.assertEqual(len(items), 1)
        self.assertEqual(items[0]["title"], "Summary after sync")

    def test_assistant_chat_uses_saved_context(self):
        workspace_id = str(uuid4())
        task_id = str(uuid4())

        self.client.post(
            "/api/ai/mentor/context",
            headers=self.headers,
            json={
                "workspaceId": workspace_id,
                "title": "Задача: Проверить CI",
                "content": "Нужно стабилизировать deploy-launch и проверить связанные задачи.",
                "kind": "task",
                "status": "approved",
                "visibility": "task-context",
                "taskId": task_id,
                "sourceRef": f"task:{task_id}",
            },
        )

        response = self.client.post(
            "/api/ai/assistant/chat",
            headers=self.headers,
            json={
                "workspaceId": workspace_id,
                "question": "Что делать по deploy-launch?",
                "assignedTaskIds": [task_id],
                "includeExternalSources": False,
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertIn("## Краткий ответ", payload["answer"])
        self.assertTrue(payload["sources"])
        self.assertEqual(payload["generationMode"], "fallback-render")
        self.assertEqual(payload["sources"][0]["taskId"], task_id)

    def test_mentor_transcribe_accepts_plain_text_payload(self):
        workspace_id = str(uuid4())

        response = self.client.post(
            "/api/ai/mentor/transcribe",
            headers=self.headers,
            json={
                "workspaceId": workspace_id,
                "transcriptText": "Созвон: согласовали дедлайн и ответственных.",
                "language": "ru",
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["status"], "draft")
        self.assertIn("дедлайн", payload["transcriptText"])

    def test_mentor_transcribe_restores_schema_after_state_cleanup(self):
        workspace_id = str(uuid4())
        shutil.rmtree(main.agent_service.config.state_dir, ignore_errors=True)

        response = self.client.post(
            "/api/ai/mentor/transcribe",
            headers=self.headers,
            json={
                "workspaceId": workspace_id,
                "transcriptText": "После очистки state каталога схема должна восстанавливаться.",
                "language": "ru",
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["status"], "draft")
        self.assertTrue((main.agent_service.config.state_dir / "assistant.db").exists())

    def test_legacy_chat_remains_available(self):
        response = self.client.post(
            "/api/ai/chat",
            headers=self.headers,
            json={
                "message": "Собери обзор проекта",
                "history": [],
                "context": {"projectId": "project-1", "tasks": [{"id": "task-1"}], "columns": [{"id": "todo"}]},
            },
        )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["action"], None)
        self.assertEqual(payload["actions"], [])
        self.assertTrue(payload["followUps"])
        self.assertIn("orchestration layer", payload["reply"])


if __name__ == "__main__":
    unittest.main()

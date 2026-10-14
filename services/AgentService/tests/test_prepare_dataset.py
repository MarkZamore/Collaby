import json
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SERVICE_DIR = Path(__file__).resolve().parents[1]
TRAINING_DIR = SERVICE_DIR / "training"
if str(TRAINING_DIR) not in sys.path:
    sys.path.insert(0, str(TRAINING_DIR))

import prepare_dataset


class _FakeHttpResponse:
    def __init__(self, payload):
        self._payload = payload

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False

    def read(self):
        return json.dumps(self._payload, ensure_ascii=False).encode("utf-8")


class PrepareDatasetTests(unittest.TestCase):
    def test_fetch_remote_snapshot_uses_internal_headers_and_url(self):
        captured = {}

        def fake_urlopen(request, timeout):
            captured["url"] = request.full_url
            captured["headers"] = dict(request.header_items())
            captured["timeout"] = timeout
            return _FakeHttpResponse({"projectId": "project-1"})

        with mock.patch.object(prepare_dataset.urllib.request, "urlopen", side_effect=fake_urlopen):
            result = prepare_dataset.fetch_remote_snapshot(
                management_service_url="http://api-management:5003/",
                internal_service_name="ai-service",
                internal_management_key="top-secret",
                project_id="project-1",
            )

        self.assertEqual(result, {"projectId": "project-1"})
        self.assertEqual(
            captured["url"],
            "http://api-management:5003/api/management/internal/v1/queries/projects/project-1/assistant-snapshot",
        )
        self.assertEqual(captured["timeout"], 15)
        self.assertEqual(captured["headers"]["X-internal-service"], "ai-service")
        self.assertEqual(captured["headers"]["X-internal-service-key"], "top-secret")

    def test_fetch_remote_snapshot_returns_none_on_404(self):
        request = prepare_dataset.urllib.request.Request("http://api-management:5003/internal")
        error = prepare_dataset.urllib.error.HTTPError(
            url=request.full_url,
            code=404,
            msg="Not Found",
            hdrs=None,
            fp=None,
        )

        with mock.patch.object(prepare_dataset.urllib.request, "urlopen", side_effect=error):
            result = prepare_dataset.fetch_remote_snapshot(
                management_service_url="http://api-management:5003",
                internal_service_name="ai-service",
                internal_management_key="top-secret",
                project_id="project-404",
            )

        self.assertIsNone(result)

    def test_merge_snapshots_prefers_later_snapshot_with_same_project_id(self):
        file_snapshot = {
            "projectId": "project-1",
            "project": {"id": "project-1", "name": "Old name"},
            "tasks": [],
        }
        remote_snapshot = {
            "projectId": "project-1",
            "project": {"id": "project-1", "name": "Fresh name"},
            "tasks": [{"id": "task-1"}],
        }
        anonymous_snapshot = {"tasks": [{"id": "task-2"}]}

        merged = prepare_dataset.merge_snapshots([file_snapshot], [remote_snapshot, anonymous_snapshot])

        self.assertEqual(len(merged), 2)
        self.assertEqual(merged[0]["project"]["name"], "Fresh name")
        self.assertEqual(merged[1]["tasks"][0]["id"], "task-2")

    def test_main_builds_non_empty_manifest_from_remote_snapshot(self):
        with tempfile.TemporaryDirectory(prefix="collaby-prepare-dataset-") as temp_dir:
            root = Path(temp_dir)
            assistant_db = root / "assistant.db"
            output_dir = root / "dataset"

            connection = sqlite3.connect(assistant_db)
            try:
                connection.execute(
                    """
                    CREATE TABLE knowledge_items (
                        id TEXT PRIMARY KEY,
                        workspace_id TEXT,
                        project_id TEXT,
                        task_id TEXT,
                        kind TEXT,
                        status TEXT,
                        title TEXT,
                        content TEXT,
                        updated_at TEXT
                    )
                    """
                )
                connection.execute(
                    """
                    CREATE TABLE transcripts (
                        id TEXT PRIMARY KEY,
                        workspace_id TEXT,
                        project_id TEXT,
                        transcript_text TEXT,
                        updated_at TEXT
                    )
                    """
                )
                connection.execute(
                    """
                    INSERT INTO knowledge_items (
                        id, workspace_id, project_id, task_id, kind, status, title, content, updated_at
                    )
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """,
                    (
                        "knowledge-1",
                        "workspace-1",
                        "project-1",
                        "task-1",
                        "approved-summary",
                        "approved",
                        "Контекст по дедлайну",
                        "Нужно закрыть задачу до конца недели и согласовать деплой.",
                        "2026-03-20T10:00:00Z",
                    ),
                )
                connection.execute(
                    """
                    INSERT INTO transcripts (
                        id, workspace_id, project_id, transcript_text, updated_at
                    )
                    VALUES (?, ?, ?, ?, ?)
                    """,
                    (
                        "transcript-1",
                        "workspace-1",
                        "project-1",
                        "Обсудили приоритеты, дедлайн и необходимость согласовать релиз.",
                        "2026-03-20T11:00:00Z",
                    ),
                )
                connection.commit()
            finally:
                connection.close()

            remote_snapshot = {
                "workspaceId": "workspace-1",
                "projectId": "project-1",
                "project": {"id": "project-1", "name": "Analytics Demo"},
                "tasks": [
                    {
                        "id": "task-1",
                        "title": "Подготовить деплой",
                        "description": "Собрать артефакты и проверить rollout на сервере.",
                        "statusName": "In Progress",
                        "comments": [{"content": "Нужно закончить до пятницы."}],
                    }
                ],
            }

            argv = [
                "prepare_dataset.py",
                "--assistant-db",
                str(assistant_db),
                "--project-id",
                "project-1",
                "--management-service-url",
                "http://api-management:5003",
                "--internal-service-name",
                "ai-service",
                "--internal-management-key",
                "top-secret",
                "--output-dir",
                str(output_dir),
            ]

            with mock.patch.object(
                prepare_dataset,
                "fetch_remote_snapshot",
                return_value=remote_snapshot,
            ):
                with mock.patch.object(sys, "argv", argv):
                    result = prepare_dataset.main()

            self.assertEqual(result, 0)

            manifest = json.loads((output_dir / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(manifest["counts"]["knowledgeItems"], 1)
            self.assertEqual(manifest["counts"]["transcripts"], 1)
            self.assertEqual(manifest["counts"]["remoteSnapshots"], 1)
            self.assertEqual(manifest["counts"]["assistant"], 1)
            self.assertGreaterEqual(manifest["counts"]["mentor"], 2)
            self.assertGreaterEqual(manifest["counts"]["merged"], 3)

            assistant_records = [
                json.loads(line)
                for line in (output_dir / "assistant-sft.jsonl").read_text(encoding="utf-8").splitlines()
                if line.strip()
            ]
            self.assertEqual(len(assistant_records), 1)
            self.assertEqual(assistant_records[0]["metadata"]["projectId"], "project-1")


if __name__ == "__main__":
    unittest.main()

"""
Готовит SFT-датасеты для `Qwen2.5-1.5B-Instruct` из repo-managed snapshot-ов и `assistant.db`.
Выходные JSONL-файлы можно напрямую использовать в Colab для `SFT + QLoRA (4-bit)`.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sqlite3
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any


MENTOR_SYSTEM_PROMPT = (
    "Ты локальный AI-ассистент тимлида Collaby. "
    "Твоя задача — собирать контекст проекта, превращать обсуждения в краткие и точные выжимки, "
    "не додумывать факты и сохранять терминологию команды."
)

ASSISTANT_SYSTEM_PROMPT = (
    "Ты локальный AI-ассистент участника Collaby. "
    "Отвечай структурированно: `## Краткий ответ`, `## Что делать дальше`, "
    "`## Связанные задачи`, `## Источники`. "
    "Опирайся на внутренний контекст и явно помечай происхождение источников."
)

DEFAULT_MANAGEMENT_SERVICE_URL = os.getenv("MANAGEMENT_SERVICE_URL", "").strip()
DEFAULT_INTERNAL_SERVICE_NAME = os.getenv("AI_INTERNAL_SERVICE_NAME", "ai-service").strip() or "ai-service"
DEFAULT_INTERNAL_MANAGEMENT_KEY = os.getenv("INTERNAL_AUTH_AI_TO_MANAGEMENT_KEY", "").strip()


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def get_value(payload: dict[str, Any], *keys: str, default: Any = None) -> Any:
    for key in keys:
        if key in payload and payload[key] not in (None, ""):
            return payload[key]
    return default


def normalize_text(value: Any) -> str:
    if value is None:
        return ""
    return " ".join(str(value).strip().split())


def short_text(value: Any, limit: int = 1200) -> str:
    normalized = normalize_text(value)
    if len(normalized) <= limit:
        return normalized
    return f"{normalized[: limit - 1].rstrip()}…"


def stable_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    digest.update(path.read_bytes())
    return digest.hexdigest()


def load_assistant_db(database_path: Path) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    if not database_path.exists():
        return [], []

    connection = sqlite3.connect(database_path)
    connection.row_factory = sqlite3.Row
    try:
        knowledge_items = [
            dict(row)
            for row in connection.execute(
                """
                SELECT *
                FROM knowledge_items
                WHERE status = 'approved'
                ORDER BY updated_at DESC
                """
            ).fetchall()
        ]
        transcripts = [
            dict(row)
            for row in connection.execute(
                """
                SELECT *
                FROM transcripts
                ORDER BY updated_at DESC
                """
            ).fetchall()
        ]
    finally:
        connection.close()

    return knowledge_items, transcripts


def build_mentor_examples(
    knowledge_items: list[dict[str, Any]],
    transcripts: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    examples: list[dict[str, Any]] = []

    for transcript in transcripts:
        transcript_text = short_text(transcript.get("transcript_text"), limit=4000)
        if not transcript_text:
            continue

        examples.append(
            {
                "messages": [
                    {"role": "system", "content": MENTOR_SYSTEM_PROMPT},
                    {
                        "role": "user",
                        "content": (
                            "Подготовь выжимку для скрытого контекста по голосовой записи. "
                            "Сохрани ключевые решения, договоренности и следующие шаги.\n\n"
                            f"Транскрипт:\n{transcript_text}"
                        ),
                    },
                    {
                        "role": "assistant",
                        "content": (
                            "Краткая выжимка:\n"
                            f"{short_text(transcript_text, limit=900)}\n\n"
                            "Следующие шаги:\n- Проверить задачи, упомянутые в записи.\n"
                            "- Подтвердить выжимку и сохранить её в скрытый контекст."
                        ),
                    },
                ],
                "metadata": {
                    "slice": "mentor",
                    "sourceKind": "transcript",
                    "workspaceId": transcript.get("workspace_id"),
                    "projectId": transcript.get("project_id"),
                },
            }
        )

    for item in knowledge_items:
        if item.get("kind") not in {"approved-summary", "note", "document"}:
            continue

        title = normalize_text(item.get("title")) or "Контекст"
        content = short_text(item.get("content"), limit=4000)
        if not content:
            continue

        examples.append(
            {
                "messages": [
                    {"role": "system", "content": MENTOR_SYSTEM_PROMPT},
                    {
                        "role": "user",
                        "content": (
                            "Собери подтверждённую выжимку для тимлида и подготовь её к сохранению в hidden context.\n\n"
                            f"Заголовок: {title}\n"
                            f"Контент:\n{content}"
                        ),
                    },
                    {
                        "role": "assistant",
                        "content": (
                            f"Заголовок: {title}\n"
                            f"Выжимка:\n{content}\n\n"
                            "Статус: готово к подтверждению и сохранению."
                        ),
                    },
                ],
                "metadata": {
                    "slice": "mentor",
                    "sourceKind": item.get("kind"),
                    "workspaceId": item.get("workspace_id"),
                    "projectId": item.get("project_id"),
                    "taskId": item.get("task_id"),
                },
            }
        )

    return examples


def extract_comments(task: dict[str, Any]) -> list[dict[str, Any]]:
    comments = get_value(task, "comments", "Comments", default=[])
    return [comment for comment in comments if isinstance(comment, dict)]


def build_task_answer(task: dict[str, Any], project_name: str, context_sources: list[str]) -> str:
    title = normalize_text(get_value(task, "title", "Title", default="Задача"))
    description = short_text(get_value(task, "description", "Description"), limit=800) or "Описание не заполнено."
    status = normalize_text(get_value(task, "statusName", "status", "StatusName")) or "Статус не указан"
    next_steps = []
    for comment in extract_comments(task)[:2]:
        comment_text = short_text(
            get_value(comment, "content", "Content", "text", "Text"),
            limit=240,
        )
        if comment_text:
            next_steps.append(f"- Учитывать комментарий: {comment_text}")
    if not next_steps:
        next_steps.append("- Уточнить следующий явный шаг у владельца задачи.")

    sources_block = "\n".join(f"- {source}" for source in context_sources) or "- internal | нет | Источники не найдены"

    return (
        "## Краткий ответ\n"
        f"По задаче «{title}» в проекте «{project_name}» сейчас важен статус: {status}. "
        f"{description}\n\n"
        "## Что делать дальше\n"
        f"{chr(10).join(next_steps)}\n\n"
        "## Связанные задачи\n"
        "- Проверить соседние задачи того же проекта и зависимые комментарии.\n\n"
        "## Источники\n"
        f"{sources_block}"
    )


def build_assistant_examples(
    snapshots: list[dict[str, Any]],
    knowledge_items: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    examples: list[dict[str, Any]] = []
    knowledge_by_project: dict[str, list[dict[str, Any]]] = {}
    for item in knowledge_items:
        project_id = normalize_text(item.get("project_id"))
        if not project_id:
            continue
        knowledge_by_project.setdefault(project_id, []).append(item)

    for snapshot in snapshots:
        project = get_value(snapshot, "project", "Project", default={}) or {}
        project_id = normalize_text(get_value(snapshot, "projectId", "ProjectId")) or normalize_text(
            get_value(project, "id", "Id")
        )
        project_name = normalize_text(get_value(project, "name", "Name")) or "Проект"
        tasks = get_value(snapshot, "tasks", "Tasks", default=[])
        if not isinstance(tasks, list):
            continue

        project_knowledge = knowledge_by_project.get(project_id, [])
        for task in tasks:
            if not isinstance(task, dict):
                continue
            title = normalize_text(get_value(task, "title", "Title"))
            if not title:
                continue

            task_id = normalize_text(get_value(task, "id", "Id"))
            task_sources = [f"internal | task | {title}"]
            for comment in extract_comments(task)[:2]:
                comment_text = short_text(get_value(comment, "content", "Content", "text", "Text"), limit=120)
                if comment_text:
                    task_sources.append(f"internal | comment | {comment_text}")
            for item in project_knowledge[:2]:
                task_sources.append(f"internal | {item.get('kind') or 'knowledge'} | {normalize_text(item.get('title'))}")

            examples.append(
                {
                    "messages": [
                        {"role": "system", "content": ASSISTANT_SYSTEM_PROMPT},
                        {
                            "role": "user",
                            "content": (
                                f"Что нужно сделать по назначенной задаче «{title}» "
                                f"в проекте «{project_name}»?"
                            ),
                        },
                        {
                            "role": "assistant",
                            "content": build_task_answer(task, project_name, task_sources),
                        },
                    ],
                    "metadata": {
                        "slice": "assistant",
                        "workspaceId": normalize_text(get_value(snapshot, "workspaceId", "WorkspaceId"))
                        or normalize_text(get_value(project, "workspaceId", "WorkspaceId")),
                        "projectId": project_id,
                        "taskId": task_id,
                    },
                }
            )

    return examples


def write_jsonl(path: Path, records: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        for record in records:
            handle.write(json.dumps(record, ensure_ascii=False))
            handle.write("\n")


def load_snapshots(snapshot_dir: Path | None) -> list[dict[str, Any]]:
    if snapshot_dir is None or not snapshot_dir.exists():
        return []

    snapshots: list[dict[str, Any]] = []
    for path in sorted(snapshot_dir.glob("*.json")):
        payload = read_json(path)
        if isinstance(payload, dict):
            payload["_sourceFile"] = path.name
            snapshots.append(payload)
    return snapshots


def fetch_remote_snapshot(
    management_service_url: str,
    internal_service_name: str,
    internal_management_key: str,
    project_id: str,
) -> dict[str, Any] | None:
    normalized_project_id = normalize_text(project_id)
    if not normalized_project_id:
        return None
    if not management_service_url:
        raise ValueError("management_service_url is required when project ids are requested")
    if not internal_management_key:
        raise ValueError("internal_management_key is required when project ids are requested")

    url = (
        f"{management_service_url.rstrip('/')}/api/management/internal/v1/queries/projects/"
        f"{normalized_project_id}/assistant-snapshot"
    )
    request = urllib.request.Request(
        url,
        headers={
            "X-Internal-Service": internal_service_name,
            "X-Internal-Service-Key": internal_management_key,
        },
        method="GET",
    )

    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        if exc.code == 404:
            return None
        raise


def merge_snapshots(*groups: list[dict[str, Any]]) -> list[dict[str, Any]]:
    merged: dict[str, dict[str, Any]] = {}
    anonymous: list[dict[str, Any]] = []
    for group in groups:
        for snapshot in group:
            project = get_value(snapshot, "project", "Project", default={}) or {}
            project_id = normalize_text(get_value(snapshot, "projectId", "ProjectId")) or normalize_text(
                get_value(project, "id", "Id")
            )
            if project_id:
                merged[project_id] = snapshot
            else:
                anonymous.append(snapshot)
    return [*merged.values(), *anonymous]


def main() -> int:
    parser = argparse.ArgumentParser(description="Prepare mentor/assistant SFT datasets for Collaby Qwen training")
    parser.add_argument("--assistant-db", type=Path, default=Path("services/AgentService/.local/assistant.db"))
    parser.add_argument("--snapshot-dir", type=Path, default=None)
    parser.add_argument("--project-id", action="append", default=[])
    parser.add_argument("--management-service-url", default=DEFAULT_MANAGEMENT_SERVICE_URL)
    parser.add_argument("--internal-service-name", default=DEFAULT_INTERNAL_SERVICE_NAME)
    parser.add_argument("--internal-management-key", default=DEFAULT_INTERNAL_MANAGEMENT_KEY)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()

    args.output_dir.mkdir(parents=True, exist_ok=True)

    knowledge_items, transcripts = load_assistant_db(args.assistant_db)
    file_snapshots = load_snapshots(args.snapshot_dir)
    remote_snapshots = [
        snapshot
        for snapshot in (
            fetch_remote_snapshot(
                management_service_url=args.management_service_url,
                internal_service_name=args.internal_service_name,
                internal_management_key=args.internal_management_key,
                project_id=project_id,
            )
            for project_id in args.project_id
        )
        if isinstance(snapshot, dict)
    ]
    snapshots = merge_snapshots(file_snapshots, remote_snapshots)

    mentor_examples = build_mentor_examples(knowledge_items, transcripts)
    assistant_examples = build_assistant_examples(snapshots, knowledge_items)
    merged_examples = mentor_examples + assistant_examples

    mentor_path = args.output_dir / "mentor-sft.jsonl"
    assistant_path = args.output_dir / "assistant-sft.jsonl"
    merged_path = args.output_dir / "collaby-qwen-sft.jsonl"

    write_jsonl(mentor_path, mentor_examples)
    write_jsonl(assistant_path, assistant_examples)
    write_jsonl(merged_path, merged_examples)

    manifest = {
        "model": "Qwen2.5-1.5B-Instruct",
        "trainingStrategy": "SFT + QLoRA (4-bit)",
        "inputs": {
            "assistantDb": str(args.assistant_db),
            "snapshotDir": str(args.snapshot_dir) if args.snapshot_dir else None,
            "projectIds": args.project_id,
            "managementServiceUrl": args.management_service_url or None,
        },
        "counts": {
            "mentor": len(mentor_examples),
            "assistant": len(assistant_examples),
            "merged": len(merged_examples),
            "knowledgeItems": len(knowledge_items),
            "transcripts": len(transcripts),
            "snapshots": len(snapshots),
            "fileSnapshots": len(file_snapshots),
            "remoteSnapshots": len(remote_snapshots),
        },
        "artifacts": {
            "mentor": {"path": str(mentor_path), "sha256": stable_sha256(mentor_path)},
            "assistant": {"path": str(assistant_path), "sha256": stable_sha256(assistant_path)},
            "merged": {"path": str(merged_path), "sha256": stable_sha256(merged_path)},
        },
    }
    (args.output_dir / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(json.dumps(manifest, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

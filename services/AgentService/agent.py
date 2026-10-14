"""
Локальный orchestration backend для AI-ассистента Collaby.
Сервис хранит скрытый контекст workspace, синхронизирует project snapshot из
внутренних сервисов и возвращает структурированные ответы без внешнего SaaS AI.
"""

from __future__ import annotations

import hashlib
import json
import os
import asyncio
import re
import sqlite3
import threading
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, quote, unquote, urlparse
from urllib.request import Request, urlopen

import httpx
import jwt

try:
    from faster_whisper import WhisperModel
except Exception:  # pragma: no cover - зависит от локального runtime и доступности native libs
    WhisperModel = None

try:
    from llama_cpp import Llama
except Exception:  # pragma: no cover - зависит от локального runtime и доступности CUDA/native libs
    Llama = None

DEFAULT_SIGNING_KEY = "collaby-local-ai-signing-key-change-me"
DEFAULT_ISSUER = "collaby-identity"
DEFAULT_AUDIENCE = "collaby-clients"
DEFAULT_MODEL_NAME = "Qwen2.5-1.5B-Instruct"
DEFAULT_INFERENCE_BACKEND = "local-qwen-gguf"
DEFAULT_LLM_CONTEXT_TOKENS = 2048
DEFAULT_LLM_GPU_LAYERS = 0
DEFAULT_ASR_DEVICE = "cpu"
DEFAULT_ASR_COMPUTE_TYPE = "int8"
SUPPORTED_ADAPTER_FORMAT = "gguf-lora"
INTERNAL_CALLER_HEADER = "X-Internal-Service"
INTERNAL_CREDENTIAL_HEADER = "X-Internal-Service-Key"

STOPWORDS = {
    "и",
    "в",
    "во",
    "на",
    "с",
    "со",
    "к",
    "ко",
    "по",
    "из",
    "что",
    "как",
    "или",
    "для",
    "про",
    "под",
    "это",
    "the",
    "and",
    "with",
    "from",
    "into",
}


def utcnow_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


def normalize_text(value: str | None) -> str:
    return re.sub(r"\s+", " ", (value or "").strip())


def normalize_multiline_text(value: str | None) -> str:
    raw = (value or "").replace("\r\n", "\n").replace("\r", "\n").strip()
    if not raw:
        return ""

    normalized_lines: list[str] = []
    previous_blank = False
    for line in raw.split("\n"):
        compact = re.sub(r"[ \t]+", " ", line).strip()
        is_blank = compact == ""
        if is_blank and previous_blank:
            continue
        normalized_lines.append(compact)
        previous_blank = is_blank

    return "\n".join(normalized_lines).strip()


def tokenize(value: str) -> list[str]:
    return [
        token
        for token in re.findall(r"[A-Za-zА-Яа-я0-9_-]{2,}", (value or "").lower())
        if token not in STOPWORDS
    ]


def first_non_empty(*values: str | None) -> str | None:
    for value in values:
        normalized = normalize_text(value)
        if normalized:
            return normalized
    return None


def slugify_segment(value: str | None, fallback: str = "item") -> str:
    normalized = re.sub(r"[^a-z0-9а-яё\s-]", "", (value or "").strip().lower())
    normalized = re.sub(r"\s+", "-", normalized)
    normalized = re.sub(r"-+", "-", normalized).strip("-")
    return normalized or fallback


def read_int_env(name: str, default: int) -> int:
    raw_value = os.getenv(name, str(default)).strip()
    try:
        return int(raw_value)
    except (TypeError, ValueError):
        return default


@dataclass(frozen=True)
class AuthenticatedPrincipal:
    user_id: str
    email: str | None
    username: str | None
    is_system_admin: bool


@dataclass(frozen=True)
class AgentConfig:
    state_dir: Path
    database_path: Path
    model_dir: Path
    adapters_dir: Path
    model_manifest_path: Path
    adapter_manifest_path: Path
    model_file_name: str
    asr_model_size: str
    llm_context_tokens: int
    llm_gpu_layers: int
    asr_device: str
    asr_compute_type: str
    public_base_url: str
    management_service_url: str
    user_service_url: str
    internal_service_name: str
    internal_management_key: str
    internal_user_key: str
    jwt_signing_key: str
    jwt_issuer: str
    jwt_audience: str
    model_name: str
    inference_backend: str
    external_search_enabled: bool

    @classmethod
    def from_environment(cls) -> "AgentConfig":
        state_dir = Path(os.getenv("AI_STATE_DIR", "/tmp/collaby-ai-state")).resolve()
        state_dir.mkdir(parents=True, exist_ok=True)
        model_dir = Path(os.getenv("AI_MODEL_DIR", str(state_dir / "models"))).resolve()
        adapters_dir = Path(os.getenv("AI_ADAPTERS_DIR", str(state_dir / "adapters"))).resolve()
        model_dir.mkdir(parents=True, exist_ok=True)
        adapters_dir.mkdir(parents=True, exist_ok=True)

        return cls(
            state_dir=state_dir,
            database_path=state_dir / "assistant.db",
            model_dir=model_dir,
            adapters_dir=adapters_dir,
            model_manifest_path=Path(os.getenv("AI_MODEL_MANIFEST_PATH", "/app/model-manifest.json")).resolve(),
            adapter_manifest_path=Path(os.getenv("AI_ADAPTER_MANIFEST_PATH", "/app/adapter-manifest.json")).resolve(),
            model_file_name=os.getenv("AI_MODEL_FILE", "qwen2.5-1.5b-instruct-q5_k_m.gguf").strip()
            or "qwen2.5-1.5b-instruct-q5_k_m.gguf",
            asr_model_size=os.getenv("AI_ASR_MODEL_SIZE", "small").strip() or "small",
            llm_context_tokens=read_int_env("AI_LLM_CONTEXT_TOKENS", DEFAULT_LLM_CONTEXT_TOKENS),
            llm_gpu_layers=read_int_env("AI_LLM_GPU_LAYERS", DEFAULT_LLM_GPU_LAYERS),
            asr_device=os.getenv("AI_ASR_DEVICE", DEFAULT_ASR_DEVICE).strip() or DEFAULT_ASR_DEVICE,
            asr_compute_type=os.getenv("AI_ASR_COMPUTE_TYPE", DEFAULT_ASR_COMPUTE_TYPE).strip()
            or DEFAULT_ASR_COMPUTE_TYPE,
            public_base_url=os.getenv("COLLABY_PUBLIC_BASE_URL", "https://collaby.org").strip() or "https://collaby.org",
            management_service_url=os.getenv("MANAGEMENT_SERVICE_URL", "http://api-management:5003").strip(),
            user_service_url=os.getenv("USER_SERVICE_URL", "http://api-users:5002").strip(),
            internal_service_name=os.getenv("AI_INTERNAL_SERVICE_NAME", "ai-service").strip() or "ai-service",
            internal_management_key=os.getenv("INTERNAL_AUTH_AI_TO_MANAGEMENT_KEY", "").strip(),
            internal_user_key=os.getenv("INTERNAL_AUTH_AI_TO_USER_KEY", "").strip(),
            jwt_signing_key=os.getenv("IDENTITY_JWT_KEY", DEFAULT_SIGNING_KEY).strip() or DEFAULT_SIGNING_KEY,
            jwt_issuer=os.getenv("IDENTITY_JWT_ISSUER", DEFAULT_ISSUER).strip() or DEFAULT_ISSUER,
            jwt_audience=os.getenv("IDENTITY_JWT_AUDIENCE", DEFAULT_AUDIENCE).strip() or DEFAULT_AUDIENCE,
            model_name=os.getenv("AI_MODEL_NAME", DEFAULT_MODEL_NAME).strip() or DEFAULT_MODEL_NAME,
            inference_backend=os.getenv("AI_INFERENCE_BACKEND", DEFAULT_INFERENCE_BACKEND).strip() or DEFAULT_INFERENCE_BACKEND,
            external_search_enabled=os.getenv("AI_EXTERNAL_SEARCH_ENABLED", "true").strip().lower() != "false",
        )


class AgentStorage:
    def __init__(self, database_path: Path) -> None:
        self._database_path = database_path
        self._database_path.parent.mkdir(parents=True, exist_ok=True)
        self._ensure_schema()

    def _connect(self) -> sqlite3.Connection:
        # Защищаемся от случаев, когда state-каталог был очищен между запросами или тестами.
        self._database_path.parent.mkdir(parents=True, exist_ok=True)
        connection = sqlite3.connect(self._database_path, timeout=30, check_same_thread=False)
        connection.row_factory = sqlite3.Row
        self._apply_schema(connection)
        return connection

    def _ensure_schema(self) -> None:
        with self._connect():
            pass

    def _apply_schema(self, connection: sqlite3.Connection) -> None:
        connection.executescript(
            """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS knowledge_items (
                id TEXT PRIMARY KEY,
                origin_key TEXT NOT NULL UNIQUE,
                workspace_id TEXT NOT NULL,
                project_id TEXT,
                task_id TEXT,
                kind TEXT NOT NULL,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                visibility TEXT NOT NULL,
                status TEXT NOT NULL,
                source_ref TEXT,
                source_url TEXT,
                author_user_id TEXT,
                origin TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                last_synced_at TEXT
            );

            CREATE TABLE IF NOT EXISTS transcripts (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                project_id TEXT,
                user_id TEXT NOT NULL,
                filename TEXT,
                language TEXT,
                transcript_text TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS chat_messages (
                id TEXT PRIMARY KEY,
                scope TEXT NOT NULL,
                workspace_id TEXT NOT NULL,
                project_id TEXT,
                user_id TEXT NOT NULL,
                session_id TEXT,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                metadata_json TEXT,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_knowledge_workspace_project
                ON knowledge_items(workspace_id, project_id, status, visibility);
            CREATE INDEX IF NOT EXISTS idx_chat_messages_scope
                ON chat_messages(scope, workspace_id, project_id, user_id, created_at);
            CREATE INDEX IF NOT EXISTS idx_transcripts_workspace
                ON transcripts(workspace_id, project_id, user_id, created_at);
            """
        )

    def upsert_knowledge_item(
        self,
        *,
        origin_key: str,
        workspace_id: str,
        project_id: str | None,
        task_id: str | None,
        kind: str,
        title: str,
        content: str,
        visibility: str,
        status: str,
        source_ref: str | None,
        source_url: str | None,
        author_user_id: str | None,
        origin: str,
    ) -> dict[str, Any]:
        timestamp = utcnow_iso()
        entity_id = str(uuid.uuid5(uuid.NAMESPACE_URL, origin_key))
        payload = {
            "id": entity_id,
            "origin_key": origin_key,
            "workspace_id": workspace_id,
            "project_id": project_id,
            "task_id": task_id,
            "kind": kind,
            "title": title,
            "content": content,
            "visibility": visibility,
            "status": status,
            "source_ref": source_ref,
            "source_url": source_url,
            "author_user_id": author_user_id,
            "origin": origin,
            "created_at": timestamp,
            "updated_at": timestamp,
            "last_synced_at": timestamp if origin == "sync" else None,
        }

        with self._connect() as connection:
            existing = connection.execute(
                "SELECT created_at FROM knowledge_items WHERE origin_key = ?",
                (origin_key,),
            ).fetchone()
            created_at = existing["created_at"] if existing else timestamp
            connection.execute(
                """
                INSERT INTO knowledge_items (
                    id, origin_key, workspace_id, project_id, task_id, kind, title, content,
                    visibility, status, source_ref, source_url, author_user_id, origin,
                    created_at, updated_at, last_synced_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(origin_key) DO UPDATE SET
                    workspace_id = excluded.workspace_id,
                    project_id = excluded.project_id,
                    task_id = excluded.task_id,
                    kind = excluded.kind,
                    title = excluded.title,
                    content = excluded.content,
                    visibility = excluded.visibility,
                    status = excluded.status,
                    source_ref = excluded.source_ref,
                    source_url = excluded.source_url,
                    author_user_id = excluded.author_user_id,
                    origin = excluded.origin,
                    updated_at = excluded.updated_at,
                    last_synced_at = excluded.last_synced_at
                """,
                (
                    entity_id,
                    origin_key,
                    workspace_id,
                    project_id,
                    task_id,
                    kind,
                    title,
                    content,
                    visibility,
                    status,
                    source_ref,
                    source_url,
                    author_user_id,
                    origin,
                    created_at,
                    timestamp,
                    timestamp if origin == "sync" else None,
                ),
            )

        payload["created_at"] = created_at
        return payload

    def save_transcript(
        self,
        *,
        workspace_id: str,
        project_id: str | None,
        user_id: str,
        filename: str | None,
        language: str | None,
        transcript_text: str,
        source_kind: str,
        status: str,
    ) -> dict[str, Any]:
        timestamp = utcnow_iso()
        transcript_id = str(uuid.uuid4())
        payload = {
            "id": transcript_id,
            "workspace_id": workspace_id,
            "project_id": project_id,
            "user_id": user_id,
            "filename": filename,
            "language": language,
            "transcript_text": transcript_text,
            "source_kind": source_kind,
            "status": status,
            "created_at": timestamp,
            "updated_at": timestamp,
        }

        with self._connect() as connection:
            connection.execute(
                """
                INSERT INTO transcripts (
                    id, workspace_id, project_id, user_id, filename, language,
                    transcript_text, source_kind, status, created_at, updated_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    transcript_id,
                    workspace_id,
                    project_id,
                    user_id,
                    filename,
                    language,
                    transcript_text,
                    source_kind,
                    status,
                    timestamp,
                    timestamp,
                ),
            )

        return payload

    def get_transcript(self, transcript_id: str) -> dict[str, Any] | None:
        with self._connect() as connection:
            row = connection.execute(
                "SELECT * FROM transcripts WHERE id = ?",
                (transcript_id,),
            ).fetchone()
        return dict(row) if row else None

    def save_chat_message(
        self,
        *,
        scope: str,
        workspace_id: str,
        project_id: str | None,
        user_id: str,
        role: str,
        content: str,
        session_id: str | None = None,
        metadata: dict[str, Any] | None = None,
    ) -> None:
        timestamp = utcnow_iso()
        with self._connect() as connection:
            connection.execute(
                """
                INSERT INTO chat_messages (
                    id, scope, workspace_id, project_id, user_id, session_id,
                    role, content, metadata_json, created_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    str(uuid.uuid4()),
                    scope,
                    workspace_id,
                    project_id,
                    user_id,
                    session_id,
                    role,
                    content,
                    json.dumps(metadata or {}, ensure_ascii=False),
                    timestamp,
                ),
            )

    def list_context_items(
        self,
        *,
        workspace_id: str,
        project_id: str | None = None,
        status: str | None = None,
        visibility: str | None = None,
        limit: int = 50,
    ) -> list[dict[str, Any]]:
        query = [
            "SELECT * FROM knowledge_items WHERE workspace_id = ?",
        ]
        parameters: list[Any] = [workspace_id]

        if project_id:
            query.append("AND (project_id = ? OR project_id IS NULL)")
            parameters.append(project_id)
        if status:
            query.append("AND status = ?")
            parameters.append(status)
        if visibility:
            query.append("AND visibility = ?")
            parameters.append(visibility)

        query.append("ORDER BY updated_at DESC LIMIT ?")
        parameters.append(limit)

        with self._connect() as connection:
            rows = connection.execute(" ".join(query), tuple(parameters)).fetchall()
        return [dict(row) for row in rows]

    def search_internal(
        self,
        *,
        workspace_id: str,
        query: str,
        project_id: str | None,
        allowed_task_ids: list[str] | None,
        include_hidden: bool,
        limit: int = 8,
    ) -> list[dict[str, Any]]:
        candidates = self.list_context_items(
            workspace_id=workspace_id,
            project_id=project_id,
            status="approved",
            limit=200,
        )

        query_tokens = tokenize(query)
        urgency_requested = self._question_requests_urgent_tasks(query)
        allowed = None if allowed_task_ids is None else {
            task_id.lower()
            for task_id in allowed_task_ids
            if task_id
        }
        scored: list[tuple[float, dict[str, Any]]] = []

        for item in candidates:
            visibility = item.get("visibility")
            task_id = (item.get("task_id") or "").lower()

            if visibility == "hidden" and not include_hidden:
                continue

            if visibility == "task-context" and allowed is not None:
                if not task_id or task_id not in allowed:
                    continue

            haystack = " ".join(
                value
                for value in [
                    item.get("title"),
                    item.get("content"),
                    item.get("kind"),
                ]
                if value
            ).lower()

            score = 0.1
            for token in query_tokens:
                if token in haystack:
                    score += 1.0
            if project_id and item.get("project_id") == project_id:
                score += 0.35
            if visibility == "hidden":
                score += 0.2
            if item.get("kind") == "approved-summary":
                score += 0.3
            if urgency_requested and item.get("kind") == "task":
                score += self._estimate_task_urgency(item.get("content"))

            if query_tokens and score <= 0.1:
                continue

            scored.append((score, item))

        scored.sort(key=lambda entry: (entry[0], entry[1].get("updated_at", "")), reverse=True)
        return [self._to_source(item, score) for score, item in scored[:limit]]

    def _extract_excerpt_field(self, excerpt: str | None, field_name: str) -> str:
        prefix = f"{field_name}:"
        for line in (excerpt or "").splitlines():
            stripped = line.strip()
            if stripped.startswith(prefix):
                return normalize_text(stripped.split(":", 1)[1])
        return ""

    def _question_requests_urgent_tasks(self, question: str | None) -> bool:
        normalized = normalize_text(question).lower()
        if not normalized:
            return False

        urgency_markers = (
            "сроч",
            "важн",
            "приор",
            "дедлайн",
            "срок",
            "urgent",
            "priority",
            "deadline",
        )
        return any(marker in normalized for marker in urgency_markers)

    def _estimate_task_urgency(self, excerpt: str | None) -> float:
        status = self._extract_excerpt_field(excerpt, "Status").lower()
        priority = self._extract_excerpt_field(excerpt, "Priority").lower()
        due_date = self._extract_excerpt_field(excerpt, "DueDate")

        done_markers = ("готово", "done", "completed", "closed", "resolved")
        if any(marker in status for marker in done_markers):
            return -3.0

        priority_score = 0.0
        if priority in {"critical", "urgent"}:
            priority_score = 3.5
        elif priority in {"high", "major"}:
            priority_score = 2.5
        elif priority in {"medium", "normal"}:
            priority_score = 1.5
        elif priority in {"low"}:
            priority_score = 0.5

        due_score = 0.0
        if due_date and due_date.lower() != "не задан":
            try:
                due_at = datetime.fromisoformat(due_date.replace("Z", "+00:00"))
                if due_at.tzinfo is None:
                    due_at = due_at.replace(tzinfo=timezone.utc)
                now_utc = datetime.now(timezone.utc)
                days_left = (due_at - now_utc).total_seconds() / 86400
                if days_left <= 0:
                    due_score = 2.5
                elif days_left <= 2:
                    due_score = 2.0
                elif days_left <= 7:
                    due_score = 1.5
                elif days_left <= 14:
                    due_score = 1.0
                else:
                    due_score = 0.2
            except ValueError:
                due_score = 0.0

        status_bonus = 0.5 if status and ("в процессе" in status or "in progress" in status) else 0.0
        return priority_score + due_score + status_bonus

    def _to_source(self, item: dict[str, Any], score: float) -> dict[str, Any]:
        return {
            "kind": item.get("kind"),
            "title": item.get("title"),
            "urlOrRef": item.get("source_url") or item.get("source_ref") or item.get("origin_key"),
            "workspaceId": item.get("workspace_id"),
            "projectId": item.get("project_id"),
            "taskId": item.get("task_id"),
            "score": round(score, 3),
            "visibility": item.get("visibility"),
            "retrievedAt": utcnow_iso(),
            "excerpt": (item.get("content") or "")[:320],
        }


class InternalServiceClients:
    def __init__(self, config: AgentConfig) -> None:
        self._config = config

    async def fetch_project_snapshot(self, project_id: str) -> dict[str, Any] | None:
        if not self._config.internal_management_key or not self._config.management_service_url:
            return None

        url = (
            f"{self._config.management_service_url.rstrip('/')}"
            f"/api/management/internal/v1/queries/projects/{project_id}/assistant-snapshot"
        )
        headers = {
            INTERNAL_CALLER_HEADER: self._config.internal_service_name,
            INTERNAL_CREDENTIAL_HEADER: self._config.internal_management_key,
        }

        async with httpx.AsyncClient(timeout=15) as client:
            response = await client.get(url, headers=headers)
            if response.status_code == 404:
                return None
            response.raise_for_status()
            return response.json()

    async def fetch_user_profiles(self, user_ids: list[str]) -> dict[str, dict[str, Any]]:
        sanitized = [user_id for user_id in user_ids if user_id]
        if not sanitized or not self._config.internal_user_key or not self._config.user_service_url:
            return {}

        url = (
            f"{self._config.user_service_url.rstrip('/')}"
            f"/api/users/internal/v1/queries/users/batch"
        )
        headers = {
            INTERNAL_CALLER_HEADER: self._config.internal_service_name,
            INTERNAL_CREDENTIAL_HEADER: self._config.internal_user_key,
        }

        async with httpx.AsyncClient(timeout=15) as client:
            response = await client.post(url, headers=headers, json=sanitized)
            response.raise_for_status()
            payload = response.json()

        result: dict[str, dict[str, Any]] = {}
        for item in payload or []:
            user_id = str(item.get("userId") or "").strip()
            if not user_id:
                continue
            result[user_id] = item
        return result


class AgentService:
    def __init__(self, config: AgentConfig | None = None) -> None:
        self.config = config or AgentConfig.from_environment()
        self.storage = AgentStorage(self.config.database_path)
        self.internal_clients = InternalServiceClients(self.config)
        self._llm_lock = threading.Lock()
        self._llm_instance = None
        self._llm_adapter_loaded = False
        self._llm_error: str | None = None
        self._asr_lock = threading.Lock()
        self._asr_instance = None
        self._asr_error: str | None = None

    def authenticate_access_token(self, token: str) -> AuthenticatedPrincipal:
        payload = jwt.decode(
            token,
            self.config.jwt_signing_key,
            algorithms=["HS256"],
            audience=self.config.jwt_audience,
            issuer=self.config.jwt_issuer,
        )

        user_id = str(payload.get("sub") or payload.get("nameid") or "").strip()
        if not user_id:
            raise jwt.InvalidTokenError("JWT does not contain subject")

        return AuthenticatedPrincipal(
            user_id=user_id,
            email=payload.get("email"),
            username=payload.get("unique_name") or payload.get("name"),
            is_system_admin=str(payload.get("is_system_admin", "")).lower() == "true",
        )

    def get_backend_metadata(self) -> dict[str, Any]:
        model_path = self.config.model_dir / self.config.model_file_name
        adapter_asset = self._load_adapter_asset()
        adapter_path = self._resolve_adapter_path()
        adapter_format = normalize_text(adapter_asset.get("format")).lower()
        adapter_runtime_compatible = adapter_format == SUPPORTED_ADAPTER_FORMAT
        return {
            "backend": "local-orchestrator",
            "state": "ready",
            "actionsEnabled": False,
            "modelName": self.config.model_name,
            "inferenceBackend": self.config.inference_backend,
            "stateDir": str(self.config.state_dir),
            "modelDir": str(self.config.model_dir),
            "modelFile": str(model_path),
            "adapterDir": str(self.config.adapters_dir),
            "adapterFile": str(adapter_path) if adapter_path is not None else None,
            "adapterFormat": adapter_format or None,
            "adapterRuntimeCompatible": adapter_runtime_compatible,
            "modelManifestPath": str(self.config.model_manifest_path),
            "adapterManifestPath": str(self.config.adapter_manifest_path),
            "modelReady": model_path.exists(),
            "adapterReady": bool(adapter_runtime_compatible and adapter_path and adapter_path.exists()),
            "modelLoaded": self._llm_instance is not None,
            "adapterLoaded": self._llm_adapter_loaded,
            "adapterSha256": adapter_asset.get("sha256"),
            "supportsLocalModelInference": Llama is not None,
            "supportsLocalAsr": WhisperModel is not None,
            "supportsVoiceUpload": True,
            "supportsExternalSearch": self.config.external_search_enabled,
            "trainingStrategy": "SFT + QLoRA (4-bit)",
        }

    def get_runtime_metadata(self) -> dict[str, Any]:
        return {
            **self.get_backend_metadata(),
            "publicBaseUrl": self.config.public_base_url,
            "managementSyncConfigured": bool(self.config.internal_management_key),
            "userDirectoryConfigured": bool(self.config.internal_user_key),
            "databasePath": str(self.config.database_path),
            "localRuntimeError": self._llm_error,
            "asrRuntimeError": self._asr_error,
            "llmContextTokens": self.config.llm_context_tokens,
            "llmGpuLayers": self.config.llm_gpu_layers,
            "asrDevice": self.config.asr_device,
            "asrComputeType": self.config.asr_compute_type,
        }

    async def mentor_chat(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str | None,
        message: str,
        history: list[dict[str, Any]],
        session_id: str | None,
    ) -> dict[str, Any]:
        sources = self.storage.search_internal(
            workspace_id=workspace_id,
            query=message,
            project_id=project_id,
            allowed_task_ids=None,
            include_hidden=True,
            limit=6,
        )
        related_context = sources[:3]
        reply = await asyncio.to_thread(self._generate_mentor_reply, message=message, project_id=project_id, related_context=related_context)

        self.storage.save_chat_message(
            scope="mentor",
            workspace_id=workspace_id,
            project_id=project_id,
            user_id=principal.user_id,
            role="user",
            content=message,
            session_id=session_id,
            metadata={"historyCount": len(history)},
        )
        self.storage.save_chat_message(
            scope="mentor",
            workspace_id=workspace_id,
            project_id=project_id,
            user_id=principal.user_id,
            role="assistant",
            content=reply,
            session_id=session_id,
            metadata={"sourceCount": len(sources)},
        )

        return {
            "reply": reply,
            "sources": sources,
            "followUps": [
                "Сформировать выжимку по последнему обсуждению",
                "Показать последние подтвержденные summary",
                "Синхронизировать проектовый контекст заново",
            ],
        }

    async def transcribe_input(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str | None,
        language: str | None,
        transcript_text: str | None,
        filename: str | None,
        audio_bytes: bytes | None,
    ) -> dict[str, Any]:
        normalized = normalize_text(transcript_text)
        source_kind = "text"

        if not normalized and audio_bytes:
            transcribed = await asyncio.to_thread(self._transcribe_audio_locally, audio_bytes, filename, language)
            if transcribed:
                normalized = transcribed
                source_kind = "audio"
            else:
                digest = hashlib.sha256(audio_bytes).hexdigest()[:12]
                normalized = (
                    f"Автоматическая транскрибация для файла {filename or 'voice-input'} пока работает "
                    f"в режиме локального skeleton-runtime. Загружен аудиофайл с отпечатком {digest}."
                )
                source_kind = "audio-placeholder"
        elif normalized:
            source_kind = "text"
        else:
            raise ValueError("Требуется transcriptText или аудиофайл")

        transcript = self.storage.save_transcript(
            workspace_id=workspace_id,
            project_id=project_id,
            user_id=principal.user_id,
            filename=filename,
            language=language,
            transcript_text=normalized,
            source_kind=source_kind,
            status="draft",
        )

        return {
            "transcriptId": transcript["id"],
            "transcriptText": transcript["transcript_text"],
            "sourceKind": transcript["source_kind"],
            "status": transcript["status"],
        }

    async def build_draft(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str | None,
        transcript_id: str | None,
        transcript_text: str | None,
        title: str | None,
        instructions: str | None,
    ) -> dict[str, Any]:
        transcript = self.storage.get_transcript(transcript_id) if transcript_id else None
        source_text = first_non_empty(transcript.get("transcript_text") if transcript else None, transcript_text)
        if not source_text:
            raise ValueError("Не удалось получить текст для выжимки")

        draft_title = first_non_empty(title, "Краткая выжимка по контексту") or "Краткая выжимка по контексту"
        summary = await asyncio.to_thread(self._summarize_text, source_text, instructions)
        preview = "\n".join(
            [
                f"# {draft_title}",
                "",
                "## Ключевая мысль",
                summary["headline"],
                "",
                "## Основные тезисы",
                *[f"- {item}" for item in summary["bullets"]],
                "",
                "## Что уточнить",
                *[f"- {item}" for item in summary["openQuestions"]],
            ]
        )

        self.storage.save_chat_message(
            scope="mentor-draft",
            workspace_id=workspace_id,
            project_id=project_id,
            user_id=principal.user_id,
            role="assistant",
            content=preview,
            metadata={"transcriptId": transcript_id},
        )

        return {
            "title": draft_title,
            "draftMarkdown": preview,
            "headline": summary["headline"],
            "bullets": summary["bullets"],
            "openQuestions": summary["openQuestions"],
            "transcriptId": transcript_id,
        }

    async def save_context(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str | None,
        task_id: str | None,
        title: str,
        content: str,
        kind: str,
        status: str,
        visibility: str,
        source_ref: str | None,
    ) -> dict[str, Any]:
        item = self.storage.upsert_knowledge_item(
            origin_key=source_ref or f"manual:{workspace_id}:{uuid.uuid4()}",
            workspace_id=workspace_id,
            project_id=project_id,
            task_id=task_id,
            kind=kind,
            title=title,
            content=content,
            visibility=visibility,
            status=status,
            source_ref=source_ref,
            source_url=f"{self.config.public_base_url.rstrip('/')}/mentor",
            author_user_id=principal.user_id,
            origin="manual",
        )
        return {
            "message": "Контекст обновлен",
            "item": item,
        }

    async def list_context(
        self,
        *,
        workspace_id: str,
        project_id: str | None,
        status: str | None,
    ) -> list[dict[str, Any]]:
        return self.storage.list_context_items(
            workspace_id=workspace_id,
            project_id=project_id,
            status=status,
            limit=100,
        )

    async def assistant_chat(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str | None,
        question: str,
        assigned_task_ids: list[str],
        include_external_sources: bool,
        history: list[dict[str, Any]],
        session_id: str | None,
    ) -> dict[str, Any]:
        internal_sources, knowledge_refreshed = await self._load_assistant_internal_sources(
            principal=principal,
            workspace_id=workspace_id,
            project_id=project_id,
            query=question,
            assigned_task_ids=assigned_task_ids,
            limit=6,
        )
        external_sources = await self._search_external(question) if include_external_sources else []
        answer, generation_mode = await asyncio.to_thread(self._generate_assistant_answer, question, internal_sources, external_sources)

        self.storage.save_chat_message(
            scope="assistant",
            workspace_id=workspace_id,
            project_id=project_id,
            user_id=principal.user_id,
            role="user",
            content=question,
            session_id=session_id,
            metadata={"historyCount": len(history), "assignedTaskIds": assigned_task_ids},
        )
        self.storage.save_chat_message(
            scope="assistant",
            workspace_id=workspace_id,
            project_id=project_id,
            user_id=principal.user_id,
            role="assistant",
            content=answer,
            session_id=session_id,
            metadata={
                "internalSourceCount": len(internal_sources),
                "externalSourceCount": len(external_sources),
                "generationMode": generation_mode,
                "knowledgeAutoRefreshed": knowledge_refreshed,
            },
        )

        return {
            "answer": answer,
            "generationMode": generation_mode,
            "sources": internal_sources + external_sources,
            "sections": {
                "summary": self._extract_first_section_text(answer),
                "nextSteps": self._extract_bullets(answer, "## Что делать дальше"),
                "relatedTasks": self._extract_bullets(answer, "## Связанные задачи"),
            },
            "followUps": [
                "Покажи связанные задачи подробнее",
                "Найди подтверждающий скрытый контекст",
                "Подключи внешние источники и сравни рекомендации",
            ],
        }

    async def assistant_search(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str | None,
        query: str,
        assigned_task_ids: list[str],
        include_external_sources: bool,
    ) -> dict[str, Any]:
        internal_sources, knowledge_refreshed = await self._load_assistant_internal_sources(
            principal=principal,
            workspace_id=workspace_id,
            project_id=project_id,
            query=query,
            assigned_task_ids=assigned_task_ids,
            limit=10,
        )
        external_sources = await self._search_external(query) if include_external_sources else []
        return {
            "items": internal_sources + external_sources,
            "knowledgeAutoRefreshed": knowledge_refreshed,
            "query": query,
        }

    async def _load_assistant_internal_sources(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str | None,
        query: str,
        assigned_task_ids: list[str],
        limit: int,
    ) -> tuple[list[dict[str, Any]], bool]:
        internal_sources = self.storage.search_internal(
            workspace_id=workspace_id,
            query=query,
            project_id=project_id,
            allowed_task_ids=assigned_task_ids,
            include_hidden=True,
            limit=limit,
        )
        refresh_for_task_context = self._should_refresh_assigned_task_context(
            project_id=project_id,
            assigned_task_ids=assigned_task_ids,
            internal_sources=internal_sources,
        )
        refresh_for_stale_source_urls = self._should_refresh_internal_source_urls(
            project_id=project_id,
            internal_sources=internal_sources,
        )
        if not refresh_for_task_context and not refresh_for_stale_source_urls:
            if internal_sources:
                return internal_sources, False
            fallback_sources = self._select_default_assistant_sources(
                principal=principal,
                workspace_id=workspace_id,
                project_id=project_id,
                assigned_task_ids=assigned_task_ids,
                limit=limit,
            )
            if not self._should_refresh_internal_source_urls(
                project_id=project_id,
                internal_sources=fallback_sources,
            ):
                return fallback_sources, False

        knowledge_refreshed = await self._best_effort_refresh_assistant_knowledge(
            principal=principal,
            workspace_id=workspace_id,
            project_id=project_id,
        )
        if not knowledge_refreshed:
            fallback_sources = self._select_default_assistant_sources(
                principal=principal,
                workspace_id=workspace_id,
                project_id=project_id,
                assigned_task_ids=assigned_task_ids,
                limit=limit,
            )
            return fallback_sources or internal_sources, False

        refreshed_sources = self.storage.search_internal(
            workspace_id=workspace_id,
            query=query,
            project_id=project_id,
            allowed_task_ids=assigned_task_ids,
            include_hidden=True,
            limit=limit,
        )
        if refreshed_sources:
            return refreshed_sources, True
        return self._select_default_assistant_sources(
            principal=principal,
            workspace_id=workspace_id,
            project_id=project_id,
            assigned_task_ids=assigned_task_ids,
            limit=limit,
        ), True

    def _select_default_assistant_sources(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str | None,
        assigned_task_ids: list[str],
        limit: int,
    ) -> list[dict[str, Any]]:
        if not project_id:
            return []

        candidates = self.storage.list_context_items(
            workspace_id=workspace_id,
            project_id=project_id,
            status="approved",
            limit=200,
        )
        if not candidates:
            return []

        allowed = {
            str(task_id).strip().lower()
            for task_id in assigned_task_ids
            if str(task_id).strip()
        }
        principal_id = normalize_text(principal.user_id).lower()
        selected: list[dict[str, Any]] = []
        seen_keys: set[str] = set()

        def append_item(item: dict[str, Any], score: float) -> None:
            origin_key = normalize_text(item.get("origin_key")) or normalize_text(item.get("id"))
            if not origin_key or origin_key in seen_keys:
                return
            seen_keys.add(origin_key)
            selected.append(self.storage._to_source(item, score))

        project_item = next((item for item in candidates if item.get("kind") == "project"), None)
        if project_item is not None:
            append_item(project_item, 0.65)

        hidden_context = [
            item
            for item in candidates
            if item.get("visibility") == "hidden"
            and item.get("kind") in {"approved-summary", "document", "note", "transcript"}
        ]
        hidden_context.sort(key=lambda item: item.get("updated_at", ""), reverse=True)
        for item in hidden_context[:2]:
            append_item(item, 0.55)

        visible_task_context = [
            item
            for item in candidates
            if item.get("visibility") == "task-context"
            and item.get("kind") in {"task", "comment"}
        ]
        if allowed:
            visible_task_context = [
                item
                for item in visible_task_context
                if normalize_text(item.get("task_id")).lower() in allowed
            ]
        else:
            visible_task_context = [
                item
                for item in visible_task_context
                if normalize_text(item.get("author_user_id")).lower() == principal_id
            ]

        visible_task_context.sort(
            key=lambda item: (
                1 if item.get("kind") == "task" else 0,
                item.get("updated_at", ""),
            ),
            reverse=True,
        )
        remaining = max(0, limit - len(selected))
        for item in visible_task_context[:remaining]:
            append_item(item, 0.45 if item.get("kind") == "task" else 0.4)

        return selected[:limit]

    def _should_refresh_assigned_task_context(
        self,
        *,
        project_id: str | None,
        assigned_task_ids: list[str],
        internal_sources: list[dict[str, Any]],
    ) -> bool:
        if not project_id:
            return False

        if not internal_sources:
            return True

        allowed = {
            str(task_id).strip().lower()
            for task_id in assigned_task_ids
            if str(task_id).strip()
        }
        if not allowed:
            return False

        for source in internal_sources:
            if normalize_text(str(source.get("visibility") or "")).lower() != "task-context":
                continue
            task_id = normalize_text(str(source.get("taskId") or "")).lower()
            if task_id and task_id in allowed:
                return False
        return True

    def _should_refresh_internal_source_urls(
        self,
        *,
        project_id: str | None,
        internal_sources: list[dict[str, Any]],
    ) -> bool:
        if not project_id or not internal_sources:
            return False

        stale_source_url = f"{self.config.public_base_url.rstrip('/')}/assistant"
        for source in internal_sources:
            kind = normalize_text(str(source.get("kind") or "")).lower()
            if kind not in {"project", "task", "comment"}:
                continue
            url_or_ref = normalize_text(str(source.get("urlOrRef") or ""))
            if url_or_ref.rstrip("/") == stale_source_url:
                return True
        return False

    async def _best_effort_refresh_assistant_knowledge(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str | None,
    ) -> bool:
        if not project_id:
            return False

        try:
            await self.sync_knowledge(
                principal=principal,
                workspace_id=workspace_id,
                project_id=project_id,
            )
        except Exception:
            return False
        return True

    async def sync_knowledge(
        self,
        *,
        principal: AuthenticatedPrincipal,
        workspace_id: str,
        project_id: str,
    ) -> dict[str, Any]:
        snapshot = await self.internal_clients.fetch_project_snapshot(project_id)
        if snapshot is None:
            raise LookupError("Project snapshot not found or internal sync is not configured")

        workspace = snapshot.get("workspace") or {}
        project = snapshot.get("project") or {}
        tasks = snapshot.get("tasks") or []
        members = snapshot.get("members") or []
        user_ids = {
            str(member.get("userId"))
            for member in members
            if member.get("userId")
        }
        for task in tasks:
            for user_key in ("assigneeId", "createdBy"):
                if task.get(user_key):
                    user_ids.add(str(task.get(user_key)))
            for comment in task.get("comments") or []:
                if comment.get("authorId"):
                    user_ids.add(str(comment.get("authorId")))

        profiles = await self.internal_clients.fetch_user_profiles(sorted(user_ids))
        saved = 0
        project_url = self._build_project_source_url(workspace, project)

        self.storage.upsert_knowledge_item(
            origin_key=f"project:{project.get('id')}",
            workspace_id=str(workspace.get("id") or workspace_id),
            project_id=str(project.get("id") or project_id),
            task_id=None,
            kind="project",
            title=f"Проект: {project.get('name') or project_id}",
            content=self._build_project_summary(project, workspace, tasks),
            visibility="hidden",
            status="approved",
            source_ref=f"project:{project.get('id')}",
            source_url=project_url,
            author_user_id=principal.user_id,
            origin="sync",
        )
        saved += 1

        for task in tasks:
            task_id = str(task.get("id"))
            assignee = profiles.get(str(task.get("assigneeId") or ""), {})
            author = profiles.get(str(task.get("createdBy") or ""), {})
            self.storage.upsert_knowledge_item(
                origin_key=f"task:{task_id}",
                workspace_id=str(workspace.get("id") or workspace_id),
                project_id=str(project.get("id") or project_id),
                task_id=task_id,
                kind="task",
                title=f"Задача: {task.get('title') or task_id}",
                content=self._build_task_summary(task, assignee, author),
                visibility="task-context",
                status="approved",
                source_ref=f"task:{task_id}",
                source_url=self._build_project_source_url(workspace, project, task_id=task_id),
                author_user_id=str(task.get("createdBy") or principal.user_id),
                origin="sync",
            )
            saved += 1

            for comment in task.get("comments") or []:
                comment_id = str(comment.get("id"))
                comment_author = profiles.get(str(comment.get("authorId") or ""), {})
                self.storage.upsert_knowledge_item(
                    origin_key=f"comment:{comment_id}",
                    workspace_id=str(workspace.get("id") or workspace_id),
                    project_id=str(project.get("id") or project_id),
                    task_id=task_id,
                    kind="comment",
                    title=f"Комментарий к задаче: {task.get('title') or task_id}",
                    content=self._build_comment_summary(task, comment, comment_author),
                    visibility="task-context",
                    status="approved",
                    source_ref=f"comment:{comment_id}",
                    source_url=self._build_project_source_url(workspace, project, task_id=task_id),
                    author_user_id=str(comment.get("authorId") or ""),
                    origin="sync",
                )
                saved += 1

        return {
            "workspaceId": str(workspace.get("id") or workspace_id),
            "projectId": str(project.get("id") or project_id),
            "savedItems": saved,
            "taskCount": len(tasks),
            "memberCount": len(members),
        }

    async def legacy_chat(
        self,
        *,
        principal: AuthenticatedPrincipal,
        message: str,
        history: list[dict[str, Any]],
        context: dict[str, Any],
    ) -> dict[str, Any]:
        _ = principal
        project_name = first_non_empty(context.get("projectName"), context.get("projectId"), "текущий проект")
        tasks = context.get("tasks") or []
        columns = context.get("columns") or []
        reply = "\n".join(
            [
                f"Сейчас AI-контур работает как локальный orchestration layer для проекта {project_name}.",
                f"В переданном board-контексте видно {len(tasks)} задач и {len(columns)} колонок.",
                "",
                "Лучший следующий шаг: откройте `/assistant` для вопросов по задачам или `/mentor` для сохранения скрытого контекста.",
            ]
        )

        follow_ups = await self.generate_follow_ups(reply)
        return {
            "reply": reply,
            "action": None,
            "params": None,
            "actions": [],
            "followUps": follow_ups,
        }

    async def generate_follow_ups(self, last_ai_message: str) -> list[str]:
        normalized = (last_ai_message or "").lower()
        if "контекст" in normalized:
            return [
                "Сформировать выжимку по последнему созвону",
                "Показать скрытый контекст по текущему проекту",
                "Синхронизировать данные задач из ManagementService",
            ]
        return [
            "Что делать дальше по назначенным задачам?",
            "Какие источники использовались в ответе?",
            "Нужно ли подключить внешние источники?",
        ]

    def _summarize_text(self, text: str, instructions: str | None) -> dict[str, Any]:
        prompt = "\n".join(
            [
                "Подготовь краткую выжимку для скрытого контекста проекта.",
                "Верни JSON с ключами headline, bullets, openQuestions.",
                f"Дополнительная инструкция: {normalize_text(instructions) or 'нет'}",
                "",
                text,
            ]
        )
        local_summary = self._generate_with_local_model(
            system_prompt="Ты локальный AI-ассистент Collaby. Пиши кратко и структурированно.",
            user_prompt=prompt,
            expect_json=True,
        )
        if isinstance(local_summary, dict):
            headline = normalize_text(str(local_summary.get("headline") or "")) or None
            bullets = [
                normalize_text(str(item))
                for item in (local_summary.get("bullets") or [])
                if normalize_text(str(item))
            ]
            open_questions = [
                normalize_text(str(item))
                for item in (local_summary.get("openQuestions") or [])
                if normalize_text(str(item))
            ]
            if headline and bullets:
                return {
                    "headline": headline,
                    "bullets": bullets[:4],
                    "openQuestions": (open_questions or ["Нужно подтвердить следующий шаг по проекту."])[:3],
                }

        sentences = [
            sentence.strip(" -\t")
            for sentence in re.split(r"[.\n\r]+", text)
            if normalize_text(sentence)
        ]
        bullets = sentences[:4] or [text[:180]]
        open_questions = [
            "Нужно подтвердить финальный приоритет и дедлайн.",
            "Нужно уточнить, какие материалы можно открыть участникам проекта.",
        ]

        if instructions:
            open_questions.insert(0, f"Учесть дополнительную установку: {normalize_text(instructions)}")

        headline = bullets[0]
        return {
            "headline": headline,
            "bullets": bullets,
            "openQuestions": open_questions[:3],
        }

    def _render_mentor_reply(
        self,
        *,
        message: str,
        project_id: str | None,
        related_context: list[dict[str, Any]],
    ) -> str:
        context_lines = [f"- {item['title']}" for item in related_context[:3]]
        body = [
            "## Краткий ответ",
            f"Локальный mentor-контур обработал запрос: {normalize_text(message)}.",
            "",
            "## Что найдено в скрытом контексте",
        ]
        body.extend(context_lines or ["- Пока нет сохраненных hidden context элементов по этому запросу."])
        body.extend(
            [
                "",
                "## Что делать дальше",
                "- Если это результат созвона или голосовой заметки, сначала отправьте его в `/api/ai/mentor/transcribe`.",
                "- Затем вызовите `/api/ai/mentor/drafts`, отредактируйте markdown и подтвердите через `/api/ai/mentor/context`.",
            ]
        )
        if project_id:
            body.append(f"- Текущий projectId: `{project_id}`.")
        return "\n".join(body)

    def _build_project_source_url(
        self,
        workspace: dict[str, Any],
        project: dict[str, Any],
        *,
        task_id: str | None = None,
    ) -> str:
        base_url = self.config.public_base_url.rstrip("/")
        workspace_route_slug = normalize_text(workspace.get("routeSlug")) or slugify_segment(
            workspace.get("name"),
            normalize_text(str(workspace.get("id") or "")) or "workspace",
        )
        project_route_key = normalize_text(project.get("routeKey")) or slugify_segment(
            project.get("name"),
            normalize_text(str(project.get("id") or "")) or "project",
        )
        route = f"{base_url}/workspace/{workspace_route_slug}/project/{project_route_key}"
        if task_id:
            return f"{route}?taskId={quote(str(task_id))}"
        return route

    def _format_priority_label(self, value: str | None) -> str:
        normalized = normalize_text(value)
        lowered = normalized.lower().replace("м", "m")
        mapping = {
            "low": "Низкий",
            "medium": "Средний",
            "normal": "Средний",
            "high": "Высокий",
            "major": "Высокий",
            "critical": "Критический",
            "urgent": "Критический",
        }
        return mapping.get(lowered, normalized)

    def _localize_priority_mentions(self, value: str | None) -> str:
        normalized = normalize_multiline_text(value)
        replacements = {
            r"\b[мm]edium\b": "Средний",
            r"\bhigh\b": "Высокий",
            r"\blow\b": "Низкий",
            r"\bcritical\b": "Критический",
            r"\burgent\b": "Критический",
            r"\bmajor\b": "Высокий",
        }
        result = normalized
        for pattern, replacement in replacements.items():
            result = re.sub(pattern, replacement, result, flags=re.IGNORECASE)
        return result

    def _is_source_dump_summary(self, value: str | None) -> bool:
        lowered = normalize_text(value).lower()
        if not lowered:
            return False

        markers = (
            "внутренний источник содержит",
            "внешний источник содержит",
            "internal source",
            "external source",
            "internal |",
            "external |",
        )
        return any(marker in lowered for marker in markers) or "|" in lowered

    def _generate_mentor_reply(
        self,
        *,
        message: str,
        project_id: str | None,
        related_context: list[dict[str, Any]],
    ) -> str:
        prompt = self._build_mentor_prompt(message=message, project_id=project_id, related_context=related_context)
        local_reply = self._generate_with_local_model(
            system_prompt="Ты локальный AI-ассистент Collaby для тимлида и научного руководителя. Отвечай в markdown с разделами.",
            user_prompt=prompt,
        )
        if local_reply:
            return local_reply
        return self._render_mentor_reply(message=message, project_id=project_id, related_context=related_context)

    def _render_assistant_answer(
        self,
        question: str,
        internal_sources: list[dict[str, Any]],
        external_sources: list[dict[str, Any]],
    ) -> str:
        question_summary = normalize_text(question)
        related_tasks_by_id: dict[str, dict[str, Any]] = {}
        for source in internal_sources:
            if source.get("kind") not in {"task", "comment"}:
                continue
            task_id = normalize_text(source.get("taskId"))
            if not task_id:
                continue
            existing = related_tasks_by_id.get(task_id)
            if existing is None or (existing.get("kind") != "task" and source.get("kind") == "task"):
                related_tasks_by_id[task_id] = source

        related_tasks = list(related_tasks_by_id.values())
        if self._question_requests_urgent_tasks(question_summary):
            related_tasks.sort(
                key=lambda source: (
                    self._estimate_task_urgency(source.get("excerpt")),
                    source.get("score") or 0,
                ),
                reverse=True,
            )

        primary_task = next((source for source in related_tasks if source.get("kind") == "task"), None)
        project_source = next((source for source in internal_sources if source.get("kind") == "project"), None)
        context_sources = [
            source
            for source in internal_sources
            if source.get("kind") in {"approved-summary", "document", "note", "transcript"}
        ]
        primary_context_source = context_sources[0] if context_sources else None
        external_titles = [
            normalize_text(source.get("title"))
            for source in external_sources
            if normalize_text(source.get("title"))
        ]

        task_title = self._source_display_title(primary_task) if primary_task else ""
        task_status = self._extract_excerpt_field(primary_task.get("excerpt") if primary_task else None, "Status")
        task_priority = self._format_priority_label(
            self._extract_excerpt_field(primary_task.get("excerpt") if primary_task else None, "Priority")
        )
        task_due_date = self._extract_excerpt_field(primary_task.get("excerpt") if primary_task else None, "DueDate")

        if primary_task and task_title:
            summary_parts = [f"Сейчас полезнее всего начать с задачи «{task_title}»."]
            if task_status and task_status.lower() != "unknown":
                summary_parts.append(f"Статус: {task_status}.")
            if task_priority and task_priority.lower() != "none":
                summary_parts.append(f"Приоритет: {task_priority}.")
            if task_due_date and task_due_date.lower() != "не задан":
                summary_parts.append(f"Дедлайн: {task_due_date}.")
            if question_summary:
                summary_parts.append(f"Это наиболее релевантная точка входа для запроса: {question_summary}.")
            summary_line = " ".join(summary_parts)
        elif project_source and normalize_text(project_source.get("excerpt")):
            project_excerpt = normalize_multiline_text(project_source.get("excerpt"))
            summary_line = (
                f"{project_excerpt} Вопрос участника: {question_summary}."
                if question_summary
                else project_excerpt
            )
        elif primary_context_source:
            context_titles = [
                self._source_display_title(source)
                for source in context_sources[:3]
                if self._source_display_title(source)
            ]
            context_excerpt = normalize_multiline_text(primary_context_source.get("excerpt"))
            context_line = context_excerpt.splitlines()[0] if context_excerpt else ""
            summary_parts: list[str] = []
            if context_titles:
                highlighted = ", ".join(f"«{title}»" for title in context_titles)
                summary_parts.append(f"В подтверждённом скрытом контексте уже есть ориентиры по запросу: {highlighted}.")
            if context_line:
                summary_parts.append(context_line)
            if question_summary:
                summary_parts.append(f"Вопрос участника: {question_summary}.")
            summary_line = " ".join(summary_parts) or (
                f"В подтверждённом скрытом контексте уже есть ориентиры по запросу: {question_summary}."
                if question_summary
                else "В подтверждённом скрытом контексте уже есть ориентиры для ответа."
            )
        elif external_titles:
            highlighted_sources = ", ".join(f"«{title}»" for title in external_titles[:2])
            summary_line = (
                f"Внутренний task-context для этого запроса не найден, "
                f"поэтому ответ опирается на внешние источники: {highlighted_sources}."
            )
        else:
            summary_line = (
                "Подходящий внутренний контекст не найден, поэтому ответ построен на общих шагах "
                "и доступных внешних источниках."
            )

        next_step_lines: list[str] = []
        if primary_task and task_title:
            next_step_lines.append(f"Проверьте текущий статус, владельца и ближайший следующий шаг по задаче «{task_title}».")
            if task_due_date and task_due_date.lower() != "не задан":
                next_step_lines.append(f"Сверьтесь с дедлайном {task_due_date} и подтвердите, что блокирующих рисков нет.")
            if len(related_tasks) > 1:
                next_step_lines.append("Просмотрите связанные задачи ниже, чтобы не потерять зависимости и смежные комментарии.")
            else:
                next_step_lines.append("Если деталей всё ещё не хватает, запросите у наставника обновлённую выжимку по проекту.")
        elif primary_context_source:
            primary_context_title = self._source_display_title(primary_context_source)
            next_step_lines.extend(
                [
                    (
                        f"Сверьтесь с подтверждённой выжимкой «{primary_context_title}» и используйте её как базовый ориентир по проекту."
                        if primary_context_title
                        else "Сверьтесь с подтверждённым скрытым контекстом и используйте его как базовый ориентир по проекту."
                    ),
                    "Если нужен ответ уровня конкретной задачи, уточните саму задачу или попросите наставника привязать hidden context к task-context.",
                ]
            )
        else:
            next_step_lines.extend(
                [
                    "Сверьтесь с назначенными задачами и последними подтверждёнными summary по проекту.",
                    "Если ответа всё ещё недостаточно, запросите у наставника обновление скрытого контекста.",
                ]
            )
        if not internal_sources and external_sources:
            if any(marker in question_summary.lower() for marker in ("github", "ci/cd", "pipeline", "release", "deploy", "релиз", "депло")):
                next_step_lines = [
                    "Сверьте pipeline, mandatory checks и rollback plan перед продовым запуском.",
                    "Перепроверьте secrets/variables и post-deploy smoke checks на реальном контуре.",
                    "Сопоставьте внешние рекомендации ниже с вашим текущим GitHub/deploy flow перед принятием решения.",
                ]
            else:
                next_step_lines = [
                    "Выберите 1-2 внешних источника ниже и сверьте их с внутренним контекстом проекта.",
                    "Выпишите только те пункты, которые действительно применимы в вашем workspace.",
                    "Если рекомендации нужно закрепить, оформите их как утверждённую summary через mentor flow.",
                ]
        if external_sources:
            next_step_lines.append("Сравните внутренний контекст с внешними источниками ниже, прежде чем принимать решение.")

        lines = [
            "## Краткий ответ",
            summary_line,
            "",
            "## Что делать дальше",
        ]
        lines.extend(f"- {step}" for step in next_step_lines)
        lines.extend(["", "## Связанные задачи"])
        if related_tasks:
            lines.extend(
                f"- {self._source_display_title(task)} (`{task['taskId']}`)"
                for task in related_tasks[:4]
            )
        else:
            if external_sources and not internal_sources:
                lines.append("- Для этого запроса не найдено внутренних task-context задач; ответ основан на внешних источниках.")
            else:
                lines.append("- В найденном контексте нет явно связанных назначенных задач.")

        lines.extend(["", "## Источники"])
        if internal_sources:
            lines.extend(
                f"- Внутренний источник: {source['title']} -> {source['urlOrRef']}"
                for source in internal_sources[:4]
            )
        if external_sources:
            lines.extend(
                f"- Внешний источник: {source['title']} -> {self._normalize_external_url(source.get('urlOrRef'))}"
                for source in external_sources[:4]
            )
        if not internal_sources and not external_sources:
            lines.append("- Источники пока не найдены.")

        return "\n".join(lines)

    def _normalize_local_model_assistant_answer(
        self,
        *,
        question: str,
        local_reply: str | None,
        internal_sources: list[dict[str, Any]],
        external_sources: list[dict[str, Any]],
    ) -> str:
        normalized_local_reply = normalize_multiline_text(local_reply)
        if not normalized_local_reply:
            return ""

        fallback_answer = self._render_assistant_answer(question, internal_sources, external_sources)
        fallback_sections: dict[str, list[str]] = {}
        fallback_headings: list[str] = []
        current_heading: str | None = None
        current_lines: list[str] = []
        for line in fallback_answer.splitlines():
            if line.startswith("## "):
                if current_heading is not None:
                    fallback_sections[current_heading] = current_lines[:]
                current_heading = line
                fallback_headings.append(line)
                current_lines = []
                continue
            if current_heading is not None:
                current_lines.append(line)
        if current_heading is not None:
            fallback_sections[current_heading] = current_lines[:]

        summary_heading = fallback_headings[0] if len(fallback_headings) > 0 else "## Summary"
        next_steps_heading = fallback_headings[1] if len(fallback_headings) > 1 else "## Next steps"
        related_tasks_heading = fallback_headings[2] if len(fallback_headings) > 2 else "## Related tasks"
        sources_heading = fallback_headings[3] if len(fallback_headings) > 3 else "## Sources"

        fallback_next_steps = [
            line.strip()[2:].strip()
            for line in fallback_sections.get(next_steps_heading, [])
            if line.strip().startswith("- ")
        ]
        fallback_related_tasks = [
            line.strip()[2:].strip()
            for line in fallback_sections.get(related_tasks_heading, [])
            if line.strip().startswith("- ")
        ]
        fallback_sources = [
            line.strip()[2:].strip()
            for line in fallback_sections.get(sources_heading, [])
            if line.strip().startswith("- ")
        ]

        summary_source_text = self._extract_first_section_text(normalized_local_reply) or normalized_local_reply
        summary_lines: list[str] = []
        for raw_line in summary_source_text.splitlines():
            line = normalize_text(raw_line)
            if not line or line.startswith("## "):
                continue
            line = re.sub(r"^\d+\.\s*", "", line)
            line = re.sub(r"^[-*•]\s*", "", line)
            line = normalize_text(line.strip("*").strip())
            if not line or line in summary_lines:
                continue
            if self._is_source_dump_summary(line):
                continue
            if line.endswith(":") and len(line) <= 40:
                continue
            summary_lines.append(line)
            if len(summary_lines) >= 4 or len(" ".join(summary_lines)) >= 420:
                break

        summary_text = self._localize_priority_mentions(" ".join(summary_lines))
        if not summary_text:
            summary_text = normalize_multiline_text("\n".join(fallback_sections.get(summary_heading, []))) or normalized_local_reply
        if self._is_source_dump_summary(summary_text):
            summary_text = normalize_multiline_text("\n".join(fallback_sections.get(summary_heading, []))) or fallback_answer
        summary_text = self._localize_priority_mentions(summary_text)
        if len(summary_text) > 700:
            summary_text = summary_text[:697].rsplit(" ", 1)[0].rstrip(".") + "..."

        lines = [
            summary_heading,
            summary_text,
            "",
            next_steps_heading,
        ]
        lines.extend(f"- {step}" for step in fallback_next_steps)
        lines.extend(["", related_tasks_heading])
        lines.extend(f"- {task}" for task in fallback_related_tasks)
        lines.extend(["", sources_heading])
        lines.extend(f"- {source}" for source in fallback_sources)
        return "\n".join(lines)

    def _generate_assistant_answer(
        self,
        question: str,
        internal_sources: list[dict[str, Any]],
        external_sources: list[dict[str, Any]],
    ) -> tuple[str, str]:
        if not internal_sources and external_sources:
            return self._render_assistant_answer(question, internal_sources, external_sources), "fallback-render"
        prompt = self._build_assistant_prompt(question, internal_sources, external_sources)
        local_reply = self._generate_with_local_model(
            system_prompt=(
                "Ты локальный AI-ассистент Collaby для участников проекта. "
                "Отвечай структурированно в markdown с разделами `## Краткий ответ`, "
                "`## Что делать дальше`, `## Связанные задачи`, `## Источники`."
            ),
            user_prompt=prompt,
        )
        normalized_local_reply = self._normalize_local_model_assistant_answer(
            question=question,
            local_reply=local_reply,
            internal_sources=internal_sources,
            external_sources=external_sources,
        )
        if self._is_usable_assistant_answer(normalized_local_reply, internal_sources):
            return normalized_local_reply, "local-model"
        if self._is_usable_assistant_answer(local_reply, internal_sources):
            return local_reply, "local-model"
        return self._render_assistant_answer(question, internal_sources, external_sources), "fallback-render"

    def _build_mentor_prompt(
        self,
        *,
        message: str,
        project_id: str | None,
        related_context: list[dict[str, Any]],
    ) -> str:
        context_lines = [
            f"- {item.get('title')}: {item.get('excerpt') or item.get('content') or ''}"
            for item in related_context[:4]
        ]
        return "\n".join(
            [
                f"projectId: {project_id or 'unknown'}",
                f"Сообщение: {normalize_text(message)}",
                "Найденный скрытый контекст:",
                *(context_lines or ["- Контекст пока не найден."]),
                "",
                "Верни markdown с разделами `## Краткий ответ`, `## Что найдено в скрытом контексте`, `## Что делать дальше`.",
            ]
        )

    def _build_assistant_prompt(
        self,
        question: str,
        internal_sources: list[dict[str, Any]],
        external_sources: list[dict[str, Any]],
    ) -> str:
        internal_lines = [
            f"- Внутренний источник: {normalize_text(item.get('title') or 'Без названия')}. "
            f"Детали: {normalize_text(item.get('excerpt') or item.get('content') or 'Контекст не указан.')}"
            for item in internal_sources[:5]
        ]
        external_lines = [
            f"- Внешний источник: {normalize_text(item.get('title') or 'Без названия')}. "
            f"Ссылка: {normalize_text(item.get('urlOrRef') or 'Ссылка недоступна.')}"
            for item in external_sources[:5]
        ]
        return "\n".join(
            [
                f"Вопрос участника: {normalize_text(question)}",
                "Внутренние источники:",
                *(internal_lines or ["- Internal source: none. Details: Context not found."]),
                "Внешние источники:",
                *(external_lines or ["- External source: none. Link: External search was not used."]),
            ]
        )

    def _load_adapter_asset(self) -> dict[str, Any]:
        try:
            manifest = json.loads(self.config.adapter_manifest_path.read_text(encoding="utf-8"))
        except (FileNotFoundError, OSError, json.JSONDecodeError):
            return {}

        return manifest.get("adapter") or {}

    def _resolve_adapter_path(self) -> Path | None:
        adapter_asset = self._load_adapter_asset()
        adapter_format = normalize_text(adapter_asset.get("format")).lower()
        if adapter_format != SUPPORTED_ADAPTER_FORMAT:
            return None

        filename = normalize_text(adapter_asset.get("filename"))
        if not filename:
            return None

        return self.config.adapters_dir / filename

    def _get_local_model(self):
        model_path = self.config.model_dir / self.config.model_file_name
        if not model_path.exists() or Llama is None:
            return None
        if self._llm_instance is not None:
            return self._llm_instance
        with self._llm_lock:
            if self._llm_instance is not None:
                return self._llm_instance
            try:
                llm_kwargs: dict[str, Any] = {
                    "model_path": str(model_path),
                    "n_ctx": self.config.llm_context_tokens,
                    "n_gpu_layers": self.config.llm_gpu_layers,
                    "verbose": False,
                }
                adapter_path = self._resolve_adapter_path()
                if adapter_path is not None and adapter_path.exists():
                    llm_kwargs["lora_path"] = str(adapter_path)
                if os.getenv("AI_REQUIRE_ADAPTER") == "true" and "lora_path" not in llm_kwargs:
                    raise RuntimeError("Required GGUF adapter is missing")
                self._llm_instance = Llama(**llm_kwargs)
                self._llm_adapter_loaded = "lora_path" in llm_kwargs
                self._llm_error = None
            except Exception as exc:  # pragma: no cover - зависит от локального runtime
                self._llm_error = str(exc)
                self._llm_instance = None
                self._llm_adapter_loaded = False
        return self._llm_instance

    def _generate_with_local_model(
        self,
        *,
        system_prompt: str,
        user_prompt: str,
        expect_json: bool = False,
    ) -> str | dict[str, Any] | None:
        model = self._get_local_model()
        if model is None:
            if os.getenv("AI_REQUIRE_ADAPTER") == "true":
                raise RuntimeError(self._llm_error or "Required adapted Qwen runtime is unavailable")
            return None
        try:
            with self._llm_lock:
                response = model.create_chat_completion(
                    messages=[
                        {"role": "system", "content": system_prompt},
                        {"role": "user", "content": user_prompt},
                    ],
                    temperature=0.2,
                    max_tokens=900 if not expect_json else 400,
                    response_format={"type": "json_object"} if expect_json else None,
                )
            raw_content = (((response.get("choices") or [{}])[0]).get("message") or {}).get("content")
            content = normalize_text(raw_content) if expect_json else normalize_multiline_text(raw_content)
            if not content:
                return None
            if expect_json:
                return json.loads(content)
            return content
        except Exception as exc:  # pragma: no cover - зависит от локального runtime
            self._llm_error = str(exc)
            if os.getenv("AI_REQUIRE_ADAPTER") == "true":
                raise RuntimeError("Local inference with the required adapter failed") from exc
            return None

    def _get_asr_model(self):
        if WhisperModel is None:
            return None
        if self._asr_instance is not None:
            return self._asr_instance
        with self._asr_lock:
            if self._asr_instance is not None:
                return self._asr_instance
            try:
                self._asr_instance = WhisperModel(
                    self.config.asr_model_size,
                    device=self.config.asr_device,
                    compute_type=self.config.asr_compute_type,
                    download_root=str(self.config.model_dir / "asr"),
                )
                self._asr_error = None
            except Exception as exc:  # pragma: no cover - зависит от локального runtime
                self._asr_error = str(exc)
                self._asr_instance = None
        return self._asr_instance

    def _transcribe_audio_locally(self, audio_bytes: bytes, filename: str | None, language: str | None) -> str | None:
        asr_model = self._get_asr_model()
        if asr_model is None:
            return None

        suffix = Path(filename or "voice-input.wav").suffix or ".wav"
        audio_dir = self.config.state_dir / "uploads"
        audio_dir.mkdir(parents=True, exist_ok=True)
        audio_path = audio_dir / f"{uuid.uuid4()}{suffix}"
        audio_path.write_bytes(audio_bytes)

        try:
            with self._asr_lock:
                segments, _ = asr_model.transcribe(str(audio_path), language=language or None, vad_filter=True)
                text = " ".join(normalize_text(segment.text) for segment in segments if normalize_text(segment.text))
            return normalize_text(text)
        except Exception as exc:  # pragma: no cover - зависит от локального runtime
            self._asr_error = str(exc)
            return None
        finally:
            audio_path.unlink(missing_ok=True)

    def _extract_bullets(self, markdown: str, section_title: str) -> list[str]:
        lines = markdown.splitlines()
        result: list[str] = []
        active = False
        for line in lines:
            if line.strip() == section_title:
                active = True
                continue
            if active and line.lstrip().startswith("## "):
                break
            if active and line.strip().startswith("- "):
                result.append(line.strip()[2:])
        return result

    def _extract_section_text(self, markdown: str, section_title: str) -> str:
        lines = markdown.splitlines()
        collected: list[str] = []
        active = False
        for line in lines:
            stripped = line.strip()
            if stripped == section_title:
                active = True
                continue
            if active and stripped.startswith("## "):
                break
            if active:
                collected.append(line.rstrip())
        return "\n".join(collected).strip()

    def _extract_first_section_text(self, markdown: str) -> str:
        lines = markdown.splitlines()
        collected: list[str] = []
        active = False
        for line in lines:
            stripped = line.strip()
            if stripped.startswith("## "):
                if active:
                    break
                active = True
                continue
            if active:
                collected.append(line.rstrip())
        return "\n".join(collected).strip()

    def _extract_excerpt_field(self, excerpt: str | None, field_name: str) -> str:
        prefix = f"{field_name}:"
        for line in (excerpt or "").splitlines():
            stripped = line.strip()
            if stripped.startswith(prefix):
                return normalize_text(stripped.split(":", 1)[1])
        return ""

    def _source_display_title(self, source: dict[str, Any] | None) -> str:
        if not source:
            return ""

        for field_name in ("Title", "Task"):
            value = self._extract_excerpt_field(source.get("excerpt"), field_name)
            if value:
                return value

        title = normalize_text(source.get("title"))
        if ": " in title:
            return title.split(": ", 1)[1]
        return title

    def _normalize_external_url(self, value: str | None) -> str:
        raw = normalize_text(value)
        if not raw:
            return ""

        if raw.startswith("//"):
            raw = f"https:{raw}"

        parsed = urlparse(raw)
        if "duckduckgo.com" in parsed.netloc.lower():
            target = parse_qs(parsed.query).get("uddg", [""])[0]
            normalized_target = normalize_text(unquote(target))
            if normalized_target:
                return normalized_target

        return raw

    def _question_requests_urgent_tasks(self, question: str | None) -> bool:
        normalized = normalize_text(question).lower()
        if not normalized:
            return False

        urgency_markers = (
            "сроч",
            "важн",
            "приор",
            "дедлайн",
            "срок",
            "urgent",
            "priority",
            "deadline",
        )
        return any(marker in normalized for marker in urgency_markers)

    def _estimate_task_urgency(self, excerpt: str | None) -> float:
        status = self._extract_excerpt_field(excerpt, "Status").lower()
        priority = self._extract_excerpt_field(excerpt, "Priority").lower()
        due_date = self._extract_excerpt_field(excerpt, "DueDate")

        done_markers = ("готово", "done", "completed", "closed", "resolved")
        if any(marker in status for marker in done_markers):
            return -3.0

        priority_score = 0.0
        if priority in {"critical", "urgent"}:
            priority_score = 3.5
        elif priority in {"high", "major"}:
            priority_score = 2.5
        elif priority in {"medium", "normal"}:
            priority_score = 1.5
        elif priority in {"low"}:
            priority_score = 0.5

        due_score = 0.0
        if due_date and due_date.lower() != "не задан":
            try:
                due_at = datetime.fromisoformat(due_date.replace("Z", "+00:00"))
                if due_at.tzinfo is None:
                    due_at = due_at.replace(tzinfo=timezone.utc)
                now_utc = datetime.now(timezone.utc)
                days_left = (due_at - now_utc).total_seconds() / 86400
                if days_left <= 0:
                    due_score = 2.5
                elif days_left <= 2:
                    due_score = 2.0
                elif days_left <= 7:
                    due_score = 1.5
                elif days_left <= 14:
                    due_score = 1.0
                else:
                    due_score = 0.2
            except ValueError:
                due_score = 0.0

        status_bonus = 0.5 if status and ("в процессе" in status or "in progress" in status) else 0.0
        return priority_score + due_score + status_bonus

    def _is_usable_assistant_answer(
        self,
        answer: str | None,
        internal_sources: list[dict[str, Any]],
    ) -> bool:
        normalized_answer = normalize_multiline_text(answer)
        if not normalized_answer:
            return False

        required_sections = (
            "## Краткий ответ",
            "## Что делать дальше",
            "## Связанные задачи",
            "## Источники",
        )
        if any(section not in normalized_answer for section in required_sections):
            return False

        summary = normalize_text(self._extract_section_text(normalized_answer, "## Краткий ответ"))
        if len(summary) < 24:
            return False

        if not self._extract_bullets(normalized_answer, "## Что делать дальше"):
            return False

        lowered = normalized_answer.lower()
        generic_fail_markers = (
            "не был полностью прочитан",
            "не содержит достаточных деталей",
            "предоставить больше информации",
        )
        if internal_sources and any(marker in lowered for marker in generic_fail_markers):
            return False

        return True

    def _build_project_summary(self, project: dict[str, Any], workspace: dict[str, Any], tasks: list[dict[str, Any]]) -> str:
        description = first_non_empty(project.get("description"), "Описание не заполнено") or "Описание не заполнено"
        return "\n".join(
            [
                f"Workspace: {workspace.get('name') or workspace.get('id')}",
                f"Project: {project.get('name') or project.get('id')}",
                f"Description: {description}",
                f"Task count: {len(tasks)}",
            ]
        )

    def _build_task_summary(
        self,
        task: dict[str, Any],
        assignee: dict[str, Any],
        author: dict[str, Any],
    ) -> str:
        labels = ", ".join(label.get("name") or label.get("id") or "" for label in task.get("labels") or [] if label)
        comments = task.get("comments") or []
        subtasks = task.get("subTasks") or []
        assignee_name = first_non_empty(assignee.get("displayName"), assignee.get("username"), task.get("assigneeId"))
        author_name = first_non_empty(author.get("displayName"), author.get("username"), task.get("createdBy"))
        return "\n".join(
            [
                f"Title: {task.get('title')}",
                f"Description: {first_non_empty(task.get('description'), 'Описание не заполнено')}",
                f"Status: {first_non_empty(task.get('statusName'), task.get('statusId'), 'unknown')}",
                f"Priority: {self._format_priority_label(task.get('priority')) or 'unknown'}",
                f"Assignee: {assignee_name or 'не назначен'}",
                f"CreatedBy: {author_name or 'unknown'}",
                f"DueDate: {task.get('dueDate') or 'не задан'}",
                f"Labels: {labels or 'нет'}",
                f"SubTasks: {len(subtasks)}",
                f"Comments: {len(comments)}",
            ]
        )

    def _build_comment_summary(
        self,
        task: dict[str, Any],
        comment: dict[str, Any],
        author: dict[str, Any],
    ) -> str:
        author_name = first_non_empty(author.get("displayName"), author.get("username"), comment.get("authorId"))
        return "\n".join(
            [
                f"Task: {task.get('title')}",
                f"Author: {author_name or 'unknown'}",
                f"CreatedAt: {comment.get('createdAt') or 'unknown'}",
                f"Comment: {first_non_empty(comment.get('text'), 'Пустой комментарий')}",
            ]
        )

    async def _search_external(self, query: str) -> list[dict[str, Any]]:
        if not self.config.external_search_enabled or not normalize_text(query):
            return []

        encoded = quote(query)
        url = f"https://duckduckgo.com/html/?q={encoded}"
        request = Request(
            url,
            headers={
                "User-Agent": "CollabyAgent/1.0 (+https://collaby.org)",
            },
        )

        try:
            with urlopen(request, timeout=10) as response:
                html = response.read().decode("utf-8", errors="ignore")
        except Exception:
            return []

        matches = re.findall(
            r'<a[^>]+class="result__a"[^>]+href="(?P<href>[^"]+)"[^>]*>(?P<title>.*?)</a>',
            html,
            flags=re.IGNORECASE | re.DOTALL,
        )

        items: list[dict[str, Any]] = []
        for href, title_html in matches[:4]:
            title = re.sub(r"<[^>]+>", "", title_html)
            normalized_href = self._normalize_external_url(href)
            items.append(
                {
                    "kind": "external",
                    "title": normalize_text(title) or normalized_href or href,
                    "urlOrRef": normalized_href or href,
                    "workspaceId": None,
                    "projectId": None,
                    "taskId": None,
                    "score": 0.4,
                    "visibility": "external",
                    "retrievedAt": utcnow_iso(),
                    "excerpt": "Внешний источник, найденный через server-side web search.",
                }
            )
        return items


"""
HTTP-контракт локального AI-сервиса Collaby.
Фронтенд ходит сюда через same-origin `/api/ai/*`.
"""

from __future__ import annotations

import logging
import os
import asyncio
from typing import Any
from uuid import uuid4

import jwt
from fastapi import Depends, FastAPI, File, HTTPException, Request, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from pydantic import BaseModel, Field
from fastapi.responses import JSONResponse

from agent import AgentService, AuthenticatedPrincipal
from tools_schema import get_tool_schemas

try:
    from opentelemetry import trace
    from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter
    from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
    from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
    from opentelemetry.sdk.metrics import MeterProvider
    from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
    from opentelemetry.sdk.resources import Resource
    from opentelemetry.sdk.trace import TracerProvider
    from opentelemetry.sdk.trace.export import BatchSpanProcessor
except ImportError:  # pragma: no cover
    trace = None
    OTLPMetricExporter = None
    OTLPSpanExporter = None
    FastAPIInstrumentor = None
    MeterProvider = None
    PeriodicExportingMetricReader = None
    Resource = None
    TracerProvider = None
    BatchSpanProcessor = None


app = FastAPI(
    title="Collaby AI Agent",
    version="2.0.0",
    docs_url="/swagger",
    openapi_url="/swagger/v1/swagger.json",
    redoc_url=None,
)
logger = logging.getLogger(__name__)
security = HTTPBearer(auto_error=False)
agent_service = AgentService()
@app.exception_handler(RuntimeError)
async def runtime_failure(request: Request, exc: RuntimeError):
    logger.error("Локальный AI runtime недоступен: %s", exc)
    return JSONResponse(status_code=503, content={"detail": "Локальная модель с обязательным адаптером недоступна. Проверьте журнал assistant."})
CORRELATION_HEADER = "X-Correlation-Id"

app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:8080",
        "http://127.0.0.1:8080",
    ],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


def configure_observability(application: FastAPI) -> None:
    otlp_endpoint = os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT", "").strip()
    if not otlp_endpoint:
        return

    if trace is None or OTLPMetricExporter is None or OTLPSpanExporter is None:
        logger.warning("OTLP endpoint is configured, but OpenTelemetry packages are unavailable.")
        return

    insecure_transport = otlp_endpoint.startswith("http://")
    resource = Resource.create({"service.name": "ai-agent"})

    tracer_provider = TracerProvider(resource=resource)
    tracer_provider.add_span_processor(
        BatchSpanProcessor(OTLPSpanExporter(endpoint=otlp_endpoint, insecure=insecure_transport))
    )
    trace.set_tracer_provider(tracer_provider)

    metric_reader = PeriodicExportingMetricReader(
        OTLPMetricExporter(endpoint=otlp_endpoint, insecure=insecure_transport)
    )
    meter_provider = MeterProvider(resource=resource, metric_readers=[metric_reader])

    from opentelemetry.metrics import set_meter_provider

    set_meter_provider(meter_provider)

    if FastAPIInstrumentor is not None:
        FastAPIInstrumentor.instrument_app(application)


@app.middleware("http")
async def correlation_middleware(request: Request, call_next):
    correlation_id = request.headers.get(CORRELATION_HEADER) or uuid4().hex
    request.state.correlation_id = correlation_id

    if trace is not None:
        current_span = trace.get_current_span()
        if current_span is not None and current_span.is_recording():
            current_span.set_attribute("collaby.correlation_id", correlation_id)

    response = await call_next(request)
    response.headers[CORRELATION_HEADER] = correlation_id
    return response


configure_observability(app)


class ProjectContext(BaseModel):
    workspaceId: str | None = None
    projectId: str | None = None
    projectName: str | None = None
    language: str | None = None
    columns: list[dict] = Field(default_factory=list)
    columnOrder: list[str] = Field(default_factory=list)
    tasks: list[dict] = Field(default_factory=list)
    users: list[dict] = Field(default_factory=list)
    labels: list[dict] = Field(default_factory=list)
    currentUser: str | None = None
    workspaces: list[dict] = Field(default_factory=list)
    stickerCatalog: list[dict] = Field(default_factory=list)
    boardStickerIds: list[str] = Field(default_factory=list)


class ChatRequest(BaseModel):
    message: str
    history: list[dict] = Field(default_factory=list)
    context: ProjectContext = Field(default_factory=ProjectContext)


class ActionCall(BaseModel):
    action: str
    params: dict = Field(default_factory=dict)


class ChatResponse(BaseModel):
    reply: str
    action: str | None = None
    params: dict | None = None
    actions: list[ActionCall] = Field(default_factory=list)
    followUps: list[str] = Field(default_factory=list)


class FollowUpRequest(BaseModel):
    lastMessage: str


class MentorChatRequest(BaseModel):
    workspaceId: str
    projectId: str | None = None
    message: str
    history: list[dict] = Field(default_factory=list)
    sessionId: str | None = None


class MentorDraftRequest(BaseModel):
    workspaceId: str
    projectId: str | None = None
    transcriptId: str | None = None
    transcriptText: str | None = None
    title: str | None = None
    instructions: str | None = None


class MentorContextRequest(BaseModel):
    workspaceId: str
    projectId: str | None = None
    taskId: str | None = None
    title: str
    content: str
    kind: str = "approved-summary"
    status: str = "approved"
    visibility: str = "hidden"
    sourceRef: str | None = None


class AssistantChatRequest(BaseModel):
    workspaceId: str
    projectId: str | None = None
    question: str
    assignedTaskIds: list[str] = Field(default_factory=list)
    includeExternalSources: bool = False
    history: list[dict] = Field(default_factory=list)
    sessionId: str | None = None


class AssistantSearchRequest(BaseModel):
    workspaceId: str
    projectId: str | None = None
    query: str
    assignedTaskIds: list[str] = Field(default_factory=list)
    includeExternalSources: bool = False


class KnowledgeSyncRequest(BaseModel):
    workspaceId: str
    projectId: str


async def get_current_principal(
    credentials: HTTPAuthorizationCredentials | None = Depends(security),
) -> AuthenticatedPrincipal:
    if credentials is None or not credentials.credentials:
        raise HTTPException(status_code=401, detail="Missing bearer token")

    try:
        return agent_service.authenticate_access_token(credentials.credentials)
    except jwt.PyJWTError as exc:
        raise HTTPException(status_code=401, detail=f"Invalid bearer token: {exc}") from exc


@app.get("/api/ai/health")
async def health() -> dict[str, Any]:
    if os.getenv("AI_REQUIRE_ADAPTER") == "true" and not agent_service._llm_adapter_loaded:
        raise HTTPException(status_code=503, detail="Required Qwen adapter runtime is not ready")
    return {
        "status": "ok",
        **agent_service.get_backend_metadata(),
        "toolSchemaCount": len(get_tool_schemas()),
    }


@app.get("/api/ai/runtime")
async def runtime(principal: AuthenticatedPrincipal = Depends(get_current_principal)) -> dict[str, Any]:
    _ = principal
    return agent_service.get_runtime_metadata()


@app.post("/api/ai/mentor/chat")
async def mentor_chat(
    request: MentorChatRequest,
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> dict[str, Any]:
    return await agent_service.mentor_chat(
        principal=principal,
        workspace_id=request.workspaceId,
        project_id=request.projectId,
        message=request.message,
        history=request.history,
        session_id=request.sessionId,
    )


@app.post("/api/ai/mentor/transcribe")
async def mentor_transcribe(
    request: Request,
    file: UploadFile | None = File(default=None),
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> dict[str, Any]:
    content_type = request.headers.get("content-type", "")
    transcript_text = None
    workspace_id = None
    project_id = None
    language = None
    filename = file.filename if file else None
    audio_bytes = await file.read() if file else None

    if content_type.startswith("multipart/form-data"):
        form = await request.form()
        transcript_text = form.get("transcriptText")
        workspace_id = form.get("workspaceId")
        project_id = form.get("projectId")
        language = form.get("language")
    else:
        payload = await request.json()
        transcript_text = payload.get("transcriptText")
        workspace_id = payload.get("workspaceId")
        project_id = payload.get("projectId")
        language = payload.get("language")

    if not workspace_id:
        raise HTTPException(status_code=422, detail="workspaceId is required")

    try:
        return await agent_service.transcribe_input(
            principal=principal,
            workspace_id=str(workspace_id),
            project_id=str(project_id) if project_id else None,
            language=str(language) if language else None,
            transcript_text=str(transcript_text) if transcript_text else None,
            filename=filename,
            audio_bytes=audio_bytes,
        )
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc


@app.post("/api/ai/mentor/drafts")
async def mentor_drafts(
    request: MentorDraftRequest,
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> dict[str, Any]:
    try:
        return await agent_service.build_draft(
            principal=principal,
            workspace_id=request.workspaceId,
            project_id=request.projectId,
            transcript_id=request.transcriptId,
            transcript_text=request.transcriptText,
            title=request.title,
            instructions=request.instructions,
        )
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc


@app.post("/api/ai/mentor/context")
async def mentor_context(
    request: MentorContextRequest,
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> dict[str, Any]:
    return await agent_service.save_context(
        principal=principal,
        workspace_id=request.workspaceId,
        project_id=request.projectId,
        task_id=request.taskId,
        title=request.title,
        content=request.content,
        kind=request.kind,
        status=request.status,
        visibility=request.visibility,
        source_ref=request.sourceRef,
    )


@app.get("/api/ai/mentor/context")
async def mentor_context_list(
    workspaceId: str,
    projectId: str | None = None,
    status: str | None = None,
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> dict[str, Any]:
    _ = principal
    return {
        "items": await agent_service.list_context(
            workspace_id=workspaceId,
            project_id=projectId,
            status=status,
        )
    }


@app.post("/api/ai/assistant/chat")
async def assistant_chat(
    request: AssistantChatRequest,
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> dict[str, Any]:
    return await agent_service.assistant_chat(
        principal=principal,
        workspace_id=request.workspaceId,
        project_id=request.projectId,
        question=request.question,
        assigned_task_ids=request.assignedTaskIds,
        include_external_sources=request.includeExternalSources,
        history=request.history,
        session_id=request.sessionId,
    )


@app.post("/api/ai/assistant/search")
async def assistant_search(
    request: AssistantSearchRequest,
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> dict[str, Any]:
    return await agent_service.assistant_search(
        principal=principal,
        workspace_id=request.workspaceId,
        project_id=request.projectId,
        query=request.query,
        assigned_task_ids=request.assignedTaskIds,
        include_external_sources=request.includeExternalSources,
    )


@app.post("/api/ai/knowledge/sync")
async def knowledge_sync(
    request: KnowledgeSyncRequest,
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> dict[str, Any]:
    try:
        return await agent_service.sync_knowledge(
            principal=principal,
            workspace_id=request.workspaceId,
            project_id=request.projectId,
        )
    except LookupError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc


@app.post("/api/ai/chat", response_model=ChatResponse)
async def ai_chat(
    request: ChatRequest,
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> ChatResponse:
    result = await agent_service.legacy_chat(
        principal=principal,
        message=request.message,
        history=request.history,
        context=request.context.model_dump(),
    )
    return ChatResponse(**result)


@app.post("/api/ai/followups")
async def ai_followups(
    request: FollowUpRequest,
    principal: AuthenticatedPrincipal = Depends(get_current_principal),
) -> dict[str, Any]:
    _ = principal
    return {"followUps": await agent_service.generate_follow_ups(request.lastMessage)}

@app.on_event("startup")
async def load_required_model():
    if os.getenv("AI_REQUIRE_ADAPTER") == "true":
        model = await asyncio.to_thread(agent_service._get_local_model)
        if model is None:
            raise RuntimeError(agent_service._llm_error or "Required Qwen model and adapter could not be loaded")

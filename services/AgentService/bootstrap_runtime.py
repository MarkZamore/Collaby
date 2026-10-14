"""
Runtime bootstrap для `api-ai`.
Скрипт materialize-ит tracked model assets в `ai-state`, а затем запускает uvicorn.
Kubernetes запускает этот bootstrap перед HTTP runtime; повторный запуск
проверяет постоянный кэш без повторной загрузки исправных файлов.
"""

from __future__ import annotations

import os
import subprocess
import sys


def env_is_truthy(name: str, default: bool = False) -> bool:
    value = os.getenv(name)
    if value is None:
        return default
    return value.strip().lower() in {"1", "true", "yes", "on"}


def build_fetch_command() -> list[str]:
    command = [sys.executable, "fetch_model_assets.py"]
    if env_is_truthy("AI_SKIP_ADAPTER_BOOTSTRAP", default=False):
        if env_is_truthy("AI_REQUIRE_ADAPTER", default=False):
            raise RuntimeError("Нельзя пропустить обязательный адаптер")
        command.append("--skip-adapter")
    return command


def _resolve_cwd() -> str:
    """Return /app inside Docker, otherwise the directory containing this script."""
    if os.path.isdir("/app"):
        return "/app"
    return os.path.dirname(os.path.abspath(__file__))


def main() -> int:
    port = os.getenv("PORT", "8100").strip() or "8100"
    cwd = _resolve_cwd()

    if not env_is_truthy("AI_SKIP_ASSET_BOOTSTRAP", default=False):
        print("Bootstrapping tracked AI assets into ai-state...", flush=True)
        subprocess.run(build_fetch_command(), cwd=cwd, check=True)

    uvicorn_command = [
        sys.executable,
        "-m",
        "uvicorn",
        "main:app",
        "--host",
        "0.0.0.0",
        "--port",
        port,
    ]
    return subprocess.call(uvicorn_command, cwd=cwd)


if __name__ == "__main__":
    raise SystemExit(main())

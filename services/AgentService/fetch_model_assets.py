"""
Repo-managed helper для materialized AI asset-ов внутри `ai-state`.
Скрипт читает tracked manifest-файлы, скачивает отсутствующие артефакты и
проверяет SHA-256, если он указан в manifest.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import time
import urllib.request
from pathlib import Path

from asset_auth import resolve_auth_headers

try:
    from faster_whisper import WhisperModel
except Exception as exc:  # pragma: no cover - зависит от runtime image и native libs
    WhisperModel = None
    FASTER_WHISPER_IMPORT_ERROR = exc
else:  # pragma: no cover - ветка зависит от runtime image
    FASTER_WHISPER_IMPORT_ERROR = None

DEFAULT_STATE_DIR = Path(os.getenv("AI_STATE_DIR", "/tmp/collaby-ai-state")).resolve()
DEFAULT_MODEL_DIR = Path(os.getenv("AI_MODEL_DIR", str(DEFAULT_STATE_DIR / "models"))).resolve()
DEFAULT_ADAPTERS_DIR = Path(os.getenv("AI_ADAPTERS_DIR", str(DEFAULT_STATE_DIR / "adapters"))).resolve()
DEFAULT_MODEL_MANIFEST = Path(os.getenv("AI_MODEL_MANIFEST_PATH", "/app/model-manifest.json")).resolve()
DEFAULT_ADAPTER_MANIFEST = Path(os.getenv("AI_ADAPTER_MANIFEST_PATH", "/app/adapter-manifest.json")).resolve()
DEFAULT_ASR_DIR = (DEFAULT_MODEL_DIR / "asr").resolve()
SUPPORTED_ADAPTER_FORMAT = "gguf-lora"


def load_manifest(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def sha256sum(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def download_file(url: str, destination: Path, headers: dict[str, str] | None = None) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(destination.suffix + ".part")
    request = urllib.request.Request(url, headers=headers or {})
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=120) as response, temporary.open("wb") as output:
                shutil.copyfileobj(response, output)
            temporary.replace(destination)
            return
        except (OSError, TimeoutError) as exc:
            temporary.unlink(missing_ok=True)
            if attempt == 2:
                raise RuntimeError(f"Загрузка {destination.name} прервана после 3 попыток; повторный запуск восстановит файл") from exc
            time.sleep(attempt + 1)


def ensure_asset(asset: dict, directory: Path) -> dict:
    filename = str(asset.get("filename") or "").strip()
    download_url = str(asset.get("downloadUrl") or "").strip()
    if not filename:
        raise ValueError("Asset manifest must define filename")
    if Path(filename).name != filename or filename in {".", ".."}:
        raise ValueError("Asset filename must be a single file name")
    if not download_url:
        raise ValueError(f"Asset {filename} does not define downloadUrl")

    target = directory / filename
    directory.mkdir(parents=True, exist_ok=True)

    expected_sha = str(asset.get("sha256") or "").strip().lower()
    expected_size = asset.get("sizeBytes")
    if target.exists() and (
        (expected_size is not None and target.stat().st_size != int(expected_size))
        or (expected_sha and sha256sum(target) != expected_sha)
    ):
        target.unlink()
    if not target.exists():
        download_headers = resolve_auth_headers(asset.get("auth"))
        download_file(download_url, target, headers=download_headers)

    actual_sha = sha256sum(target)
    if expected_sha and actual_sha != expected_sha:
        target.unlink(missing_ok=True)
        raise ValueError(
            f"Checksum mismatch for {filename}: expected {expected_sha}, got {actual_sha}"
        )
    if expected_size is not None and target.stat().st_size != int(expected_size):
        target.unlink(missing_ok=True)
        raise ValueError(f"Size mismatch for {filename}: expected {expected_size} bytes")

    return {
        "path": str(target),
        "sha256": actual_sha,
        "sizeBytes": target.stat().st_size,
    }


def ensure_adapter_asset(asset: dict, directory: Path) -> dict:
    asset_format = str(asset.get("format") or "").strip().lower()
    if asset_format != SUPPORTED_ADAPTER_FORMAT:
        raise ValueError(
            f"Adapter manifest must declare format={SUPPORTED_ADAPTER_FORMAT!r}, got {asset_format or '<empty>'!r}"
        )

    result = ensure_asset(asset, directory)
    result["format"] = asset_format
    return result


def directory_size_bytes(path: Path) -> int:
    if not path.exists():
        return 0
    return sum(entry.stat().st_size for entry in path.rglob("*") if entry.is_file())


def ensure_asr_asset(asset: dict, directory: Path) -> dict | None:
    provider = str(asset.get("provider") or "").strip()
    if not provider:
        return None
    if provider != "faster-whisper":
        raise ValueError(f"Unsupported ASR provider: {provider}")
    if WhisperModel is None:
        raise RuntimeError(
            f"faster-whisper is unavailable in the current runtime: {FASTER_WHISPER_IMPORT_ERROR}"
        )

    model_size = str(asset.get("modelSize") or "").strip() or "small"
    directory.mkdir(parents=True, exist_ok=True)

    # Прогреваем локальный ASR-кэш заранее, чтобы первая voice-запись не ждала download на проде.
    asr_model = WhisperModel(
        model_size,
        device="cpu",
        compute_type="int8",
        download_root=str(directory),
    )
    del asr_model

    file_count = sum(1 for entry in directory.rglob("*") if entry.is_file())
    return {
        "path": str(directory),
        "provider": provider,
        "modelSize": model_size,
        "sizeBytes": directory_size_bytes(directory),
        "fileCount": file_count,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Ensure tracked AI model assets exist inside ai-state")
    parser.add_argument("--model-manifest", type=Path, default=DEFAULT_MODEL_MANIFEST)
    parser.add_argument("--adapter-manifest", type=Path, default=DEFAULT_ADAPTER_MANIFEST)
    parser.add_argument("--model-dir", type=Path, default=DEFAULT_MODEL_DIR)
    parser.add_argument("--adapters-dir", type=Path, default=DEFAULT_ADAPTERS_DIR)
    parser.add_argument("--asr-dir", type=Path, default=DEFAULT_ASR_DIR)
    parser.add_argument("--skip-adapter", action="store_true")
    args = parser.parse_args()

    model_manifest = load_manifest(args.model_manifest)
    model_result = ensure_asset(model_manifest["model"], args.model_dir)
    asr_result = ensure_asr_asset(model_manifest.get("asr") or {}, args.asr_dir)

    adapter_result = None
    if not args.skip_adapter:
        adapter_manifest = load_manifest(args.adapter_manifest)
        adapter_asset = adapter_manifest.get("adapter") or {}
        adapter_result = ensure_adapter_asset(adapter_asset, args.adapters_dir)

    payload = {
        "model": model_result,
        "asr": asr_result,
        "adapter": adapter_result,
    }
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())

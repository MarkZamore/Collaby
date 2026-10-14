"""
Готовит tracked-compatible manifest для GGUF LoRA-адаптера после Colab export.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
SERVICE_DIR = Path(__file__).resolve().parents[1]
for candidate in (SCRIPT_DIR, SERVICE_DIR):
    if str(candidate) not in sys.path:
        sys.path.insert(0, str(candidate))

from asset_auth import normalize_auth_config

SUPPORTED_ADAPTER_FORMAT = "gguf-lora"


def sha256sum(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_adapter_file(path: Path) -> None:
    if not path.exists():
        raise FileNotFoundError(f"Adapter file does not exist: {path}")
    if path.suffix.lower() != ".gguf":
        raise ValueError(
            "Runtime accepts only GGUF LoRA adapters. Convert the Colab PEFT export to .gguf before publishing the manifest."
        )


def build_manifest(
    adapter_file: Path,
    version: str,
    download_url: str,
    auth_config: dict[str, str] | None = None,
) -> dict:
    validate_adapter_file(adapter_file)
    adapter_payload = {
        "adapter": {
            "id": "collaby-qwen2.5-1.5b-lora",
            "displayName": "Collaby Qwen mentor+assistant adapter",
            "version": version,
            "status": "ready",
            "format": SUPPORTED_ADAPTER_FORMAT,
            "filename": adapter_file.name,
            "downloadUrl": download_url,
            "sha256": sha256sum(adapter_file),
            "sizeBytes": adapter_file.stat().st_size,
            "notes": "Экспортировано из Google Colab после SFT + QLoRA (4-bit) и конвертировано в GGUF LoRA для llama.cpp runtime.",
        }
    }
    normalized_auth = normalize_auth_config(auth_config)
    if normalized_auth is not None:
        adapter_payload["adapter"]["auth"] = normalized_auth
    return adapter_payload


def main() -> int:
    parser = argparse.ArgumentParser(description="Export Collaby adapter manifest")
    parser.add_argument("--adapter-file", type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--download-url", required=True)
    parser.add_argument("--auth-type")
    parser.add_argument("--auth-env-var")
    parser.add_argument("--auth-header")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    auth_config = None
    if args.auth_type:
        auth_config = {
            "type": args.auth_type,
        }
        if args.auth_env_var:
            auth_config["envVar"] = args.auth_env_var
        if args.auth_header:
            auth_config["header"] = args.auth_header

    payload = build_manifest(
        adapter_file=args.adapter_file,
        version=args.version,
        download_url=args.download_url,
        auth_config=auth_config,
    )
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

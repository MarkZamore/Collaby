"""
Готовит repo-managed bundle для Google Colab из уже собранного Qwen dataset-а.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from pathlib import Path
from typing import Any


TRAINING_DIR = Path(__file__).resolve().parent
SERVICE_DIR = TRAINING_DIR.parent
REQUIRED_DATASET_FILES = (
    "manifest.json",
    "mentor-sft.jsonl",
    "assistant-sft.jsonl",
    "collaby-qwen-sft.jsonl",
)


def sha256sum(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def ensure_dataset_dir(dataset_dir: Path) -> None:
    missing = [name for name in REQUIRED_DATASET_FILES if not (dataset_dir / name).exists()]
    if missing:
        missing_text = ", ".join(sorted(missing))
        raise FileNotFoundError(f"Dataset directory is missing required files: {missing_text}")


def copy_file(source: Path, target: Path) -> dict[str, Any]:
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    return {
        "path": str(target),
        "sha256": sha256sum(target),
        "sizeBytes": target.stat().st_size,
    }


def build_bundle(
    dataset_dir: Path,
    output_dir: Path,
    bundle_name: str,
) -> dict[str, Any]:
    ensure_dataset_dir(dataset_dir)

    bundle_root = output_dir / bundle_name
    if bundle_root.exists():
        shutil.rmtree(bundle_root)
    bundle_root.mkdir(parents=True, exist_ok=True)

    dataset_manifest = json.loads((dataset_dir / "manifest.json").read_text(encoding="utf-8"))
    dataset_target_dir = bundle_root / "dataset"
    runtime_target_dir = bundle_root / "runtime"
    scripts_target_dir = bundle_root / "scripts"
    notebooks_target_dir = bundle_root / "notebooks"

    copied_dataset = {
        name: copy_file(dataset_dir / name, dataset_target_dir / name)
        for name in REQUIRED_DATASET_FILES
    }
    copied_runtime = {
        "modelManifest": copy_file(
            SERVICE_DIR / "model-manifest.json",
            runtime_target_dir / "model-manifest.json",
        ),
        "adapterManifestTemplate": copy_file(
            SERVICE_DIR / "adapter-manifest.json",
            runtime_target_dir / "adapter-manifest.template.json",
        ),
    }
    copied_support = {
        "exportAdapterManifest": copy_file(
            TRAINING_DIR / "export_adapter_manifest.py",
            scripts_target_dir / "export_adapter_manifest.py",
        ),
        "publishAdapterPackage": copy_file(
            TRAINING_DIR / "publish_adapter_package.py",
            scripts_target_dir / "publish_adapter_package.py",
        ),
        "assetAuthHelper": copy_file(
            SERVICE_DIR / "asset_auth.py",
            scripts_target_dir / "asset_auth.py",
        ),
        "colabNotebook": copy_file(
            TRAINING_DIR / "qwen_qlora_colab.ipynb",
            notebooks_target_dir / "qwen_qlora_colab.ipynb",
        ),
        "trainingReadme": copy_file(
            TRAINING_DIR / "README.md",
            bundle_root / "README.md",
        ),
    }

    bundle_manifest = {
        "bundle": {
            "name": bundle_name,
            "model": dataset_manifest.get("model", "Qwen2.5-1.5B-Instruct"),
            "trainingStrategy": dataset_manifest.get("trainingStrategy", "SFT + QLoRA (4-bit)"),
            "datasetCounts": dataset_manifest.get("counts", {}),
            "datasetInputs": dataset_manifest.get("inputs", {}),
        },
        "dataset": copied_dataset,
        "runtime": copied_runtime,
        "support": copied_support,
    }

    manifest_path = bundle_root / "bundle-manifest.json"
    manifest_path.write_text(json.dumps(bundle_manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    bundle_manifest["bundle"]["manifest"] = {
        "path": str(manifest_path),
        "sha256": sha256sum(manifest_path),
        "sizeBytes": manifest_path.stat().st_size,
    }
    manifest_path.write_text(json.dumps(bundle_manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    archive_path = Path(shutil.make_archive(str(bundle_root), "zip", root_dir=output_dir, base_dir=bundle_name))
    bundle_manifest["bundle"]["archive"] = {
        "path": str(archive_path),
        "sha256": sha256sum(archive_path),
        "sizeBytes": archive_path.stat().st_size,
    }
    manifest_path.write_text(json.dumps(bundle_manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    return bundle_manifest


def main() -> int:
    parser = argparse.ArgumentParser(description="Package Collaby Qwen training bundle for Google Colab")
    parser.add_argument("--dataset-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--bundle-name", default="collaby-qwen-colab-bundle")
    args = parser.parse_args()

    args.output_dir.mkdir(parents=True, exist_ok=True)
    payload = build_bundle(
        dataset_dir=args.dataset_dir,
        output_dir=args.output_dir,
        bundle_name=args.bundle_name,
    )
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

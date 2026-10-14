import sys
import tempfile
import unittest
from pathlib import Path


SERVICE_DIR = Path(__file__).resolve().parents[1]
TRAINING_DIR = SERVICE_DIR / "training"
if str(TRAINING_DIR) not in sys.path:
    sys.path.insert(0, str(TRAINING_DIR))

import export_adapter_manifest


class ExportAdapterManifestTests(unittest.TestCase):
    def test_build_manifest_marks_gguf_lora_format(self):
        with tempfile.TemporaryDirectory(prefix="collaby-adapter-manifest-") as temp_dir:
            adapter_file = Path(temp_dir) / "adapter.gguf"
            adapter_file.write_bytes(b"gguf-adapter")

            payload = export_adapter_manifest.build_manifest(
                adapter_file=adapter_file,
                version="2026-03-20",
                download_url="https://example.com/adapter.gguf",
            )

            self.assertEqual(payload["adapter"]["format"], "gguf-lora")
            self.assertEqual(payload["adapter"]["filename"], "adapter.gguf")
            self.assertEqual(payload["adapter"]["status"], "ready")

    def test_build_manifest_rejects_non_gguf_adapter(self):
        with tempfile.TemporaryDirectory(prefix="collaby-adapter-manifest-") as temp_dir:
            adapter_file = Path(temp_dir) / "adapter.zip"
            adapter_file.write_bytes(b"zip")

            with self.assertRaisesRegex(ValueError, "GGUF LoRA adapters"):
                export_adapter_manifest.build_manifest(
                    adapter_file=adapter_file,
                    version="2026-03-20",
                    download_url="https://example.com/adapter.zip",
                )

    def test_build_manifest_can_embed_env_header_auth_contract(self):
        with tempfile.TemporaryDirectory(prefix="collaby-adapter-manifest-") as temp_dir:
            adapter_file = Path(temp_dir) / "adapter.gguf"
            adapter_file.write_bytes(b"gguf-adapter")

            payload = export_adapter_manifest.build_manifest(
                adapter_file=adapter_file,
                version="2026-03-20",
                download_url="https://assets.example.com/adapter.gguf",
                auth_config={
                    "type": "env-header",
                    "header": "Authorization",
                    "envVar": "ASSET_TOKEN",
                },
            )

            self.assertEqual(payload["adapter"]["auth"]["type"], "env-header")
            self.assertEqual(payload["adapter"]["auth"]["envVar"], "ASSET_TOKEN")
            self.assertEqual(payload["adapter"]["auth"]["header"], "Authorization")


if __name__ == "__main__":
    unittest.main()

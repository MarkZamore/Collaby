import hashlib
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SERVICE_DIR = Path(__file__).resolve().parents[1]
if str(SERVICE_DIR) not in sys.path:
    sys.path.insert(0, str(SERVICE_DIR))

import fetch_model_assets
from bootstrap_runtime import build_fetch_command
from fetch_model_assets import ensure_adapter_asset, ensure_asr_asset, ensure_asset


class FetchModelAssetsTests(unittest.TestCase):
    def test_ensure_asset_downloads_local_uri_and_verifies_checksum(self):
        with tempfile.TemporaryDirectory(prefix="collaby-ai-assets-") as temp_dir:
            root = Path(temp_dir)
            source = root / "source.gguf"
            source.write_bytes(b"test-model")
            expected_sha = hashlib.sha256(source.read_bytes()).hexdigest()

            target_dir = root / "models"
            asset = {
                "filename": "model.gguf",
                "downloadUrl": source.resolve().as_uri(),
                "sha256": expected_sha,
            }

            result = ensure_asset(asset, target_dir)

            self.assertEqual(result["sha256"], expected_sha)
            self.assertTrue((target_dir / "model.gguf").exists())

    def test_build_fetch_command_honors_skip_adapter_flag(self):
        original = os.environ.get("AI_SKIP_ADAPTER_BOOTSTRAP")
        os.environ["AI_SKIP_ADAPTER_BOOTSTRAP"] = "true"
        try:
            command = build_fetch_command()
        finally:
            if original is None:
                os.environ.pop("AI_SKIP_ADAPTER_BOOTSTRAP", None)
            else:
                os.environ["AI_SKIP_ADAPTER_BOOTSTRAP"] = original

        self.assertEqual(command[-1], "--skip-adapter")

    def test_ensure_asr_asset_prefetches_faster_whisper_cache(self):
        with tempfile.TemporaryDirectory(prefix="collaby-ai-asr-") as temp_dir:
            asr_dir = Path(temp_dir) / "asr"

            class FakeWhisperModel:
                def __init__(self, model_size, device, compute_type, download_root):
                    self.model_size = model_size
                    self.device = device
                    self.compute_type = compute_type
                    self.download_root = Path(download_root)
                    self.download_root.mkdir(parents=True, exist_ok=True)
                    (self.download_root / "model.bin").write_bytes(b"fake-asr-model")

            with mock.patch.object(fetch_model_assets, "WhisperModel", FakeWhisperModel):
                result = ensure_asr_asset(
                    {"provider": "faster-whisper", "modelSize": "small"},
                    asr_dir,
                )

            self.assertEqual(result["provider"], "faster-whisper")
            self.assertEqual(result["modelSize"], "small")
            self.assertEqual(result["fileCount"], 1)
            self.assertGreater(result["sizeBytes"], 0)
            self.assertTrue((asr_dir / "model.bin").exists())

    def test_ensure_asr_asset_rejects_unsupported_provider(self):
        with tempfile.TemporaryDirectory(prefix="collaby-ai-asr-") as temp_dir:
            asr_dir = Path(temp_dir) / "asr"

            with self.assertRaisesRegex(ValueError, "Unsupported ASR provider"):
                ensure_asr_asset({"provider": "unknown"}, asr_dir)

    def test_ensure_adapter_asset_requires_gguf_lora_format(self):
        with tempfile.TemporaryDirectory(prefix="collaby-ai-adapter-") as temp_dir:
            root = Path(temp_dir)
            adapter_file = root / "adapter.gguf"
            adapter_file.write_bytes(b"adapter")
            adapter_sha = hashlib.sha256(adapter_file.read_bytes()).hexdigest()

            with self.assertRaisesRegex(ValueError, "gguf-lora"):
                ensure_adapter_asset(
                    {
                        "filename": "adapter.gguf",
                        "downloadUrl": adapter_file.resolve().as_uri(),
                        "sha256": adapter_sha,
                        "format": "peft-directory",
                    },
                    root / "adapters",
                )

    def test_ensure_asset_uses_private_token_auth_header(self):
        with tempfile.TemporaryDirectory(prefix="collaby-ai-assets-auth-") as temp_dir:
            root = Path(temp_dir)
            target_dir = root / "models"
            destination = target_dir / "model.gguf"
            expected_sha = hashlib.sha256(b"secured-model").hexdigest()
            captured = {}

            def fake_urlopen(request, timeout=None):
                captured["headers"] = dict(request.header_items())

                class FakeResponse:
                    def __init__(self):
                        self._chunks = [b"secured-model", b""]

                    def __enter__(self):
                        return self

                    def __exit__(self, exc_type, exc, tb):
                        return False

                    def read(self, size=-1):
                        return self._chunks.pop(0)

                return FakeResponse()

            asset = {
                "filename": "model.gguf",
                "downloadUrl": "https://assets.example.com/adapter.gguf",
                "sha256": expected_sha,
                "auth": {
                    "type": "env-header",
                    "header": "Authorization",
                    "envVar": "TEST_ASSET_TOKEN",
                },
            }

            with mock.patch.dict(os.environ, {"TEST_ASSET_TOKEN": "secret-token"}, clear=False):
                with mock.patch.object(fetch_model_assets.urllib.request, "urlopen", side_effect=fake_urlopen):
                    result = ensure_asset(asset, target_dir)

            self.assertEqual(result["sha256"], expected_sha)
            self.assertTrue(destination.exists())
            self.assertEqual(captured["headers"]["Authorization"], "secret-token")

    def test_corrupt_cache_and_interrupted_part_are_recovered(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.gguf"
            source.write_bytes(b"valid-model")
            cache = root / "cache"
            cache.mkdir()
            (cache / "model.gguf").write_bytes(b"corrupt")
            (cache / "model.gguf.part").write_bytes(b"interrupted")
            result = ensure_asset({"filename": "model.gguf", "downloadUrl": source.as_uri(),
                                   "sha256": hashlib.sha256(b"valid-model").hexdigest(), "sizeBytes": 11}, cache)
            self.assertEqual(result["sizeBytes"], 11)
            self.assertFalse((cache / "model.gguf.part").exists())

    def test_failed_download_does_not_publish_partial_file(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "model.gguf"
            with mock.patch.object(fetch_model_assets.urllib.request, "urlopen", side_effect=OSError("offline")), mock.patch.object(fetch_model_assets.time, "sleep"):
                with self.assertRaisesRegex(RuntimeError, "3 попыток"):
                    fetch_model_assets.download_file("https://example.com/model.gguf", target)
            self.assertFalse(target.exists())
            self.assertFalse(target.with_suffix(".gguf.part").exists())


if __name__ == "__main__":
    unittest.main()

import json
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


SERVICE_DIR = Path(__file__).resolve().parents[1]
TRAINING_DIR = SERVICE_DIR / "training"
if str(TRAINING_DIR) not in sys.path:
    sys.path.insert(0, str(TRAINING_DIR))

import package_colab_bundle


class PackageColabBundleTests(unittest.TestCase):
    def test_ensure_dataset_dir_rejects_missing_files(self):
        with tempfile.TemporaryDirectory(prefix="collaby-bundle-missing-") as temp_dir:
            dataset_dir = Path(temp_dir)
            (dataset_dir / "manifest.json").write_text("{}", encoding="utf-8")

            with self.assertRaisesRegex(FileNotFoundError, "assistant-sft.jsonl"):
                package_colab_bundle.ensure_dataset_dir(dataset_dir)

    def test_build_bundle_copies_dataset_and_support_files(self):
        with tempfile.TemporaryDirectory(prefix="collaby-bundle-") as temp_dir:
            root = Path(temp_dir)
            dataset_dir = root / "dataset"
            output_dir = root / "out"
            dataset_dir.mkdir(parents=True, exist_ok=True)

            manifest_payload = {
                "model": "Qwen2.5-1.5B-Instruct",
                "trainingStrategy": "SFT + QLoRA (4-bit)",
                "counts": {"mentor": 2, "assistant": 4, "merged": 6},
                "inputs": {"projectIds": ["project-1"]},
            }
            (dataset_dir / "manifest.json").write_text(
                json.dumps(manifest_payload, ensure_ascii=False, indent=2),
                encoding="utf-8",
            )
            for name in ("mentor-sft.jsonl", "assistant-sft.jsonl", "collaby-qwen-sft.jsonl"):
                (dataset_dir / name).write_text('{"messages":[],"metadata":{}}\n', encoding="utf-8")

            payload = package_colab_bundle.build_bundle(
                dataset_dir=dataset_dir,
                output_dir=output_dir,
                bundle_name="bundle-smoke",
            )

            bundle_root = output_dir / "bundle-smoke"
            self.assertTrue((bundle_root / "dataset" / "collaby-qwen-sft.jsonl").exists())
            self.assertTrue((bundle_root / "runtime" / "model-manifest.json").exists())
            self.assertTrue((bundle_root / "runtime" / "adapter-manifest.template.json").exists())
            self.assertTrue((bundle_root / "scripts" / "export_adapter_manifest.py").exists())
            self.assertTrue((bundle_root / "scripts" / "publish_adapter_package.py").exists())
            self.assertTrue((bundle_root / "scripts" / "asset_auth.py").exists())
            self.assertTrue((bundle_root / "notebooks" / "qwen_qlora_colab.ipynb").exists())
            self.assertTrue((bundle_root / "README.md").exists())
            self.assertTrue((bundle_root / "bundle-manifest.json").exists())
            self.assertTrue(Path(payload["bundle"]["archive"]["path"]).exists())
            self.assertEqual(payload["bundle"]["datasetCounts"]["assistant"], 4)

            with zipfile.ZipFile(payload["bundle"]["archive"]["path"]) as archive:
                names = set(archive.namelist())

            self.assertIn("bundle-smoke/dataset/collaby-qwen-sft.jsonl", names)
            self.assertIn("bundle-smoke/runtime/model-manifest.json", names)
            self.assertIn("bundle-smoke/scripts/export_adapter_manifest.py", names)
            self.assertIn("bundle-smoke/scripts/publish_adapter_package.py", names)
            self.assertIn("bundle-smoke/scripts/asset_auth.py", names)
            self.assertIn("bundle-smoke/notebooks/qwen_qlora_colab.ipynb", names)


if __name__ == "__main__":
    unittest.main()

"""Проверки отказа публикации при секретах, включая бинарные файлы."""
import importlib.util
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('verify_runtime', ROOT/'scripts/verify-runtime.py')
scanner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scanner)

class PublicationScanTests(unittest.TestCase):
    def test_binary_assets_are_scanned_and_values_are_redacted(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            asset = Path(folder)/'test.gguf'
            value = b'test-private-value-123456789'
            asset.write_bytes(b'\x00\xff'+value)
            result = scanner.scan_secrets([asset], [value])
            self.assertEqual(result, [asset.relative_to(ROOT).as_posix()])
            self.assertNotIn(value.decode(), str(result))
            asset.write_bytes(b'\x00\xffpublic-binary-data')
            self.assertEqual(scanner.scan_secrets([asset], [value]), [])

    def test_token_patterns_reject_source_without_local_config(self):
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            source = Path(folder)/'source.txt'
            source.write_text('ghp_' + 'a'*36)
            self.assertTrue(scanner.scan_secrets([source]))
            source.write_text('PASSWORD=${PASSWORD}\nhttp://localhost:8080')
            self.assertEqual(scanner.scan_secrets([source]), [])

if __name__ == '__main__':
    unittest.main()

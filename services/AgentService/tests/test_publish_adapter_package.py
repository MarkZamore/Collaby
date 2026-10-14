import json,sys,tempfile,unittest,urllib.error
from pathlib import Path
from unittest import mock
TRAINING=Path(__file__).resolve().parents[1]/"training"
sys.path.insert(0,str(TRAINING))
import publish_release

class ReleaseTests(unittest.TestCase):
    def test_publishes_asset_and_public_manifest(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter=Path(directory)/"adapter.gguf";adapter.write_bytes(b"GGUF-test")
            output=Path(directory)/"manifest.json"
            release={"upload_url":"https://uploads.github.com/repos/example/repo/releases/1/assets{?name,label}","assets":[]}
            responses=[urllib.error.HTTPError("url",404,"missing",None,None),release,{"browser_download_url":"https://github.com/example/repo/releases/download/adapter-v1/adapter.gguf"},{"id":2}]
            with mock.patch.object(publish_release,"api",side_effect=responses) as api:
                result=publish_release.publish(adapter,"v1","example/repo","test-token",output)
            self.assertEqual(result["adapter"]["sizeBytes"],9)
            self.assertNotIn("auth",result["adapter"])
            self.assertEqual(api.call_args_list[1].args[2],"POST")
            self.assertIn("tag_name",json.loads(api.call_args_list[1].args[3]))
            self.assertIn("name=adapter.gguf",api.call_args_list[2].args[0])
            self.assertEqual(json.loads(output.read_text())["adapter"]["downloadUrl"],result["adapter"]["downloadUrl"])

    def test_does_not_overwrite_published_asset(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter=Path(directory)/"adapter.gguf";adapter.write_bytes(b"GGUF")
            with mock.patch.object(publish_release,"api",return_value={"assets":[{"name":"adapter.gguf"}]}) as api:
                with self.assertRaises(ValueError):publish_release.publish(adapter,"v1","example/repo","test-token",Path(directory)/"manifest.json")
                self.assertEqual(api.call_count,1)

    def test_invalid_input_is_rejected_before_any_github_mutation(self):
        with tempfile.TemporaryDirectory() as directory:
            adapter=Path(directory)/"adapter.gguf";adapter.write_bytes(b"GGUF")
            with mock.patch.object(publish_release,"api") as api:
                for version,repo in [('v1','../bad'),('bad/tag','example/repo')]:
                    with self.assertRaises(ValueError):publish_release.publish(adapter,version,repo,"test-token",Path(directory)/"manifest.json")
                with self.assertRaises(FileNotFoundError):publish_release.publish(Path(directory)/"missing.gguf",'v1','example/repo',"test-token",Path(directory)/"manifest.json")
                api.assert_not_called()

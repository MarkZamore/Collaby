import asyncio
import dataclasses
import json
import os
import sys
import tempfile
import threading
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import agent


class RequiredRuntimeTests(unittest.TestCase):
    def create_service(self, root, adapter=True):
        config = dataclasses.replace(agent.AgentConfig.from_environment(), state_dir=root,
                                     database_path=root / "assistant.db", model_dir=root,
                                     adapters_dir=root, adapter_manifest_path=root / "adapter.json")
        (root / config.model_file_name).write_bytes(b"model")
        config.adapter_manifest_path.write_text(json.dumps({"adapter": {"format": "gguf-lora", "filename": "adapter.gguf"}}))
        if adapter:
            (root / "adapter.gguf").write_bytes(b"adapter")
        with patch.object(agent, "AgentStorage"):
            return agent.AgentService(config)

    def test_adapter_constructor_failure_never_retries_without_lora(self):
        with tempfile.TemporaryDirectory() as directory, patch.dict(os.environ, {"AI_REQUIRE_ADAPTER": "true"}):
            service = self.create_service(Path(directory))
            with patch.object(agent, "Llama", side_effect=TypeError("lora unsupported")) as llama:
                with self.assertRaisesRegex(RuntimeError, "lora unsupported"):
                    service._generate_with_local_model(system_prompt="test", user_prompt="test")
            self.assertEqual(llama.call_count, 1)
            self.assertTrue(llama.call_args.kwargs["lora_path"].endswith("adapter.gguf"))

    def test_required_adapter_missing_fails_before_loading_base_model(self):
        with tempfile.TemporaryDirectory() as directory, patch.dict(os.environ, {"AI_REQUIRE_ADAPTER": "true"}):
            service = self.create_service(Path(directory), adapter=False)
            with patch.object(agent, "Llama") as llama:
                with self.assertRaisesRegex(RuntimeError, "adapter is missing"):
                    service._generate_with_local_model(system_prompt="test", user_prompt="test")
                llama.assert_not_called()

    def test_native_generation_runs_outside_http_event_loop(self):
        async def scenario(service):
            started, release = threading.Event(), threading.Event()

            def blocking_generate(**kwargs):
                started.set()
                release.wait(3)
                return "answer"

            with patch.object(service, "_generate_mentor_reply", side_effect=blocking_generate):
                principal = agent.AuthenticatedPrincipal(user_id="tester", email="tester@example.com", username="tester", is_system_admin=False)
                task = asyncio.create_task(service.mentor_chat(message="hello", project_id=None,
                                            principal=principal, workspace_id=None, history=[], session_id=None))
                await asyncio.to_thread(started.wait, 1)
                self.assertTrue(started.is_set())
                self.assertFalse(task.done())
                release.set()
                await task

        with tempfile.TemporaryDirectory() as directory:
            asyncio.run(scenario(self.create_service(Path(directory))))

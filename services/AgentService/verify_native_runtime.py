"""Smoke настоящих native библиотек: декодер WAV, затем Qwen/LoRA и ASR из постоянного кэша."""
import argparse
import hashlib
import io
import json
import wave
from pathlib import Path


def silence_wav():
    audio = io.BytesIO()
    with wave.open(audio, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(16000)
        wav.writeframes(bytes(32000))
    return audio.getvalue()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--decode-only", action="store_true")
    parser.add_argument("--audio-file", type=Path)
    args = parser.parse_args()
    from faster_whisper.audio import decode_audio
    audio = silence_wav()
    samples = decode_audio(io.BytesIO(audio))
    assert len(samples) == 16000, "WAV decoder returned invalid audio length"
    if args.decode_only:
        print(json.dumps({"audioDecoder": "ok", "sampleCount": len(samples)}))
        return

    from agent import AgentService
    service = AgentService()
    model = service._get_local_model()
    assert model is not None, service._llm_error
    assert service._llm_adapter_loaded, "GGUF adapter was not loaded"
    assert model._lora_adapter is not None, "Native llama.cpp adapter handle is missing"
    adapter = service._resolve_adapter_path()
    expected = service._load_adapter_asset()["sha256"]
    checksum = hashlib.sha256(adapter.read_bytes()).hexdigest()
    assert checksum == expected, "Loaded adapter checksum mismatch"
    response = model.create_chat_completion(
        messages=[{"role": "user", "content": "What is 2 + 2? Answer with one number."}],
        max_tokens=16, temperature=0,
    )["choices"][0]["message"]["content"]
    assert response.strip(), "Qwen did not generate a response"
    assert service.config.asr_device == "cpu" and service.config.asr_compute_type == "int8"
    transcription = service._transcribe_audio_locally(
        args.audio_file.read_bytes() if args.audio_file else audio,
        args.audio_file.name if args.audio_file else "silence.wav", "en",
    )
    assert transcription is not None, service._asr_error
    print(json.dumps({"modelLoaded": True, "adapterLoaded": True, "adapterSha256": checksum,
                      "response": response, "asrDevice": "cpu", "asrComputeType": "int8",
                      "audioDecoder": "ok", "transcription": transcription}))


if __name__ == "__main__":
    main()

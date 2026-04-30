import argparse
import json
import os
import sys
from pathlib import Path


def _configure_environment() -> None:
    pyannote_models = Path(__file__).parent / "Models" / "pyannote"
    pyannote_models.mkdir(parents=True, exist_ok=True)
    os.environ["HF_HOME"] = str(pyannote_models)
    snapshots = pyannote_models / "hub" / "models--pyannote--speaker-diarization-3.1" / "snapshots"
    if snapshots.exists():
        os.environ["HF_HUB_OFFLINE"] = "1"


def transcribe(audio_path: str, model: str = "canary", **kwargs) -> list[dict]:
    from services.transcription_service import TranscriptionService
    return TranscriptionService().transcribe(audio_path, model, **kwargs)


if __name__ == "__main__":
    print("Python started", file=sys.stderr, flush=True)
    _configure_environment()

    parser = argparse.ArgumentParser()
    parser.add_argument("audio_path")
    parser.add_argument("--model", default="canary", choices=["canary", "parakeet"])
    parser.add_argument("--language", default="cs", choices=["cs", "en"])
    args = parser.parse_args()

    try:
        result = transcribe(args.audio_path, args.model, language=args.language)
        print(json.dumps({"segments": result}))
    except Exception as ex:
        print(json.dumps({"error": str(ex)}), file=sys.stderr)
        sys.exit(1)

import sys
print("Python started", file=sys.stderr, flush=True)

import os
from pathlib import Path

_pyannote_models = Path(__file__).parent / "Models" / "pyannote"
_pyannote_models.mkdir(parents=True, exist_ok=True)
os.environ["HF_HOME"] = str(_pyannote_models)
os.environ["HF_HUB_OFFLINE"] = "1"

import argparse
import json
import sys

from services.transcription_service import TranscriptionService


def transcribe(audio_path: str, model: str = "canary", **kwargs) -> list[dict]:
    return TranscriptionService().transcribe(audio_path, model, **kwargs)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("audio_path")
    parser.add_argument("--model",    default="canary", choices=["canary"])
    parser.add_argument("--language", default="cs",     choices=["cs", "en"])
    args = parser.parse_args()
    
    try:
        result = transcribe(args.audio_path, args.model, language=args.language)
        print(json.dumps({"segments": result}))
    except Exception as ex:
        print(json.dumps({"error": str(ex)}), file=sys.stderr)
        sys.exit(1)

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

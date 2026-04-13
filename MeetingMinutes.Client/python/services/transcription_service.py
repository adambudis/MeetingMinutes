import os
import sys
import contextlib
import tempfile
from pathlib import Path

import librosa
import soundfile as sf
import nemo.collections.asr as nemo_asr

from utils import progress, get_device
from services.diarization_service import DiarizationService


SAMPLE_RATE = 16000
MIN_DUR = 0.3


def _models_root() -> Path:
    return Path(__file__).parent.parent / "Models"


_LOCAL_NEMO = _models_root() / "canary-1b-v2.nemo"


def _load_canary_model():
    device = get_device()
    with contextlib.redirect_stdout(sys.stderr):
        if not _LOCAL_NEMO.exists():
            raise FileNotFoundError(f"Canary model nenalezen: {_LOCAL_NEMO}")
        progress(f"Načítám Canary model z lokálního souboru ({_LOCAL_NEMO.name})...")
        model = nemo_asr.models.ASRModel.restore_from(str(_LOCAL_NEMO))
        if device == "cuda":
            model = model.cuda()

        model.eval()
    return model


def _canary(audio_path: str, diarizer: DiarizationService, language: str = "cs", **_) -> list[dict]:
    turns = diarizer.get_speaker_turns(audio_path)
    if not turns:
        return []

    model = _load_canary_model()

    y_full, sr_native = sf.read(audio_path, dtype="float32", always_2d=False)
    if y_full.ndim > 1:
        y_full = y_full.mean(axis=1)
    if sr_native != SAMPLE_RATE:
        y_full = librosa.resample(y_full, orig_sr=sr_native, target_sr=SAMPLE_RATE)

    valid_turns = [(s, e, spk) for s, e, spk in turns if (e - s) >= MIN_DUR]
    if not valid_turns:
        return []

    temp_files: list[str] = []
    try:
        for s, e, _ in valid_turns:
            tmp = tempfile.NamedTemporaryFile(suffix=".wav", delete=False)
            sf.write(tmp.name, y_full[int(s * SAMPLE_RATE):int(e * SAMPLE_RATE)], SAMPLE_RATE)
            tmp.close()
            temp_files.append(tmp.name)

        progress(f"Přepisuji {len(valid_turns)} segmentů s Canary...")
        with contextlib.redirect_stdout(sys.stderr):
            transcriptions = model.transcribe(temp_files, source_lang=language, target_lang=language)
    finally:
        for f in temp_files:
            try:
                os.unlink(f)
            except OSError:
                pass

    result = []
    for (s, e, spk), hyp in zip(valid_turns, transcriptions):
        text = (hyp.text if hasattr(hyp, "text") else str(hyp)).strip()
        if text:
            result.append({"start": s, "end": e, "speaker": spk, "text": text})

    progress(f"Nalezeno {len(result)} segmentů.")
    return result


_BACKENDS: dict[str, callable] = {
    "canary": _canary,
}


class TranscriptionService:

    def __init__(self):
        self._diarizer = DiarizationService()

    def transcribe(self, audio_path: str, backend: str = "canary", **kwargs) -> list[dict]:
        if backend not in _BACKENDS:
            raise ValueError(f"Unknown backend '{backend}'. Available: {list(_BACKENDS)}")
        return _BACKENDS[backend](audio_path, self._diarizer, **kwargs)

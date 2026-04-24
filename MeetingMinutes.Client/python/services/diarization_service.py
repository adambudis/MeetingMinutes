import os
import numpy as np
import torch
import soundfile as sf
from pyannote.audio import Pipeline

from utils import progress, get_device

_pipeline: Pipeline | None = None


def _get_pipeline() -> Pipeline:
    global _pipeline
    if _pipeline is None:
        progress("Načítám pyannote diarizační model...")
        token = os.environ.get("HF_TOKEN")
        _pipeline = Pipeline.from_pretrained("pyannote/speaker-diarization-3.1", token=token)
        device = torch.device(get_device())
        _pipeline.to(device)
    return _pipeline


def _peak_normalize(waveform: np.ndarray) -> np.ndarray:
    """Peak-normalize to 0.9 so pyannote's VAD detects speech in quiet mic recordings."""
    peak = np.abs(waveform).max()
    if peak < 1e-6:
        return waveform
    return waveform * (0.9 / peak)


class DiarizationService:

    def get_speaker_turns(self, audio_path: str) -> list[tuple[float, float, str]]:
        pipeline = _get_pipeline()

        progress("Diarizuji audio...")
        waveform, sr = sf.read(audio_path, dtype="float32", always_2d=True)
        waveform = _peak_normalize(waveform)
        audio_tensor = {"waveform": torch.tensor(waveform.T), "sample_rate": sr}
        result = pipeline(audio_tensor)
        annotation = result.speaker_diarization if hasattr(result, "speaker_diarization") else result

        raw_turns = [
            (turn.start, turn.end, speaker)
            for turn, _, speaker in annotation.itertracks(yield_label=True)
        ]

        if not raw_turns:
            duration = waveform.shape[0] / sr
            progress("Diarizace nenalezla žádné úseky — přepisuji celý soubor jako jednoho mluvčího.")
            return [(0.0, duration, "SPEAKER_00")]

        all_speakers = sorted({spk for _, _, spk in raw_turns})
        speaker_map  = {spk: f"SPEAKER_{i:02d}" for i, spk in enumerate(all_speakers)}

        progress(f"Pyannote detekoval {len(all_speakers)} mluvčích v {len(raw_turns)} úsecích.")
        return [(s, e, speaker_map[spk]) for s, e, spk in raw_turns]

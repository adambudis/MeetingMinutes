import torch
import soundfile as sf
from pyannote.audio import Pipeline

from utils import progress, get_device

_pipeline: Pipeline | None = None


def _get_pipeline() -> Pipeline:
    global _pipeline
    if _pipeline is None:
        progress("Načítám pyannote diarizační model...")
        _pipeline = Pipeline.from_pretrained("pyannote/speaker-diarization-3.1")
        device = torch.device(get_device())
        _pipeline.to(device)
    return _pipeline


class DiarizationService:

    def get_speaker_turns(self, audio_path: str) -> list[tuple[float, float, str]]:
        pipeline = _get_pipeline()

        progress("Diarizuji audio...")
        waveform, sr = sf.read(audio_path, dtype="float32", always_2d=True)
        audio_tensor = {"waveform": torch.tensor(waveform.T), "sample_rate": sr}
        result = pipeline(audio_tensor)
        annotation = result.speaker_diarization if hasattr(result, "speaker_diarization") else result

        raw_turns = [
            (turn.start, turn.end, speaker)
            for turn, _, speaker in annotation.itertracks(yield_label=True)
        ]
        
        if not raw_turns:
            return []

        all_speakers = sorted({spk for _, _, spk in raw_turns})
        speaker_map  = {spk: f"SPEAKER_{i:02d}" for i, spk in enumerate(all_speakers)}

        progress(f"Pyannote detekoval {len(all_speakers)} mluvčích v {len(raw_turns)} úsecích.")
        return [(s, e, speaker_map[spk]) for s, e, spk in raw_turns]

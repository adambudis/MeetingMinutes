import sys


def progress(msg: str):
    print(msg, file=sys.stderr, flush=True)


def get_device() -> str:
    import torch
    return "cuda" if torch.cuda.is_available() else "cpu"

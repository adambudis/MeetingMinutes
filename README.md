# MeetingMinutes Client

## Návod k rozjetí na devu

### Požadavky
- .NET 10 SDK
- Python (3.x)
- Ollama

1. Stáhnout a nainstalovat Ollama: https://ollama.com/download
- Musí běžet lokálně na portu `11434` (default)
```
ollama pull gemma3:4b
```

2. Nastavit venv a nainstalovat požadované python balíčky:
```
cd MeetingMinutes.Client/python
python -m venv .venv
.venv/Scripts/activate
pip install -r requirements.txt
```

3. Stáhnout `canary-1b-v2.nemo` model na stránce `https://huggingface.co/nvidia/canary-1b-v2/tree/main` a umístit do složky `MeetingMinutes.Client\python\Models`.

## Návod k vytvoření exe

1. V terminálu ve složce `MeetingMinutes.Client` spustit příkaz:
```
dotnet publish -c Release -r win-x64 --self-contained true
```

2. Zkopírovat celou python/ složku vedle exe do publish složky: `bin/Release/net10.0-windows/win-x64/publish/`

3. Nyní je aplikace spustitelná, `publish/` výslednou složku lze zabalit do zipu a poslat uživatelům

4. Na cílových počítačích musí být nainstalovaný Ollama s modelem `gemma3:4b`
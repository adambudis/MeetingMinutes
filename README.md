# MeetingMinutes Client — Requirements

## Ollama

- Install: https://ollama.com/download
- Must be running locally on port `11434` (default)

```
ollama pull gemma3:4b
```

## Návod ke spuštění

Na devu
- .NET 10 SDK
- Python (3.x)

1. Nastavit venv a nainstalovat požadované balíčky:
```
cd MeetingMinutes.Client/python
python -m venv .venv
.venv/Scripts/activate
pip install -r requirements.txt
```

2. Stáhnout `canary-1b-v2.nemo` model na stránce `https://huggingface.co/nvidia/canary-1b-v2/tree/main` a umístit do složky `MeetingMinutes.Client\python\Models`.

## Vytvoření exe

1. V terminálu ve složce `MeetingMinutes.Client` spustit příkaz:
```
dotnet publish -c Release -r win-x64 --self-contained true
```

2. Zkopírovat celou python/ složku vedle exe do publish složky: `bin/Release/net10.0-windows/win-x64/publish/`

3. Nyní je aplikace spustitelná, `publish/` výslednou složku lze zabalit do zipu a poslat uživatelům
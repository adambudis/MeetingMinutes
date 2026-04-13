# MeetingMinutes Client — Requirements

## Ollama

- Install: https://ollama.com/download
- Must be running locally on port `11434` (default)

```
ollama pull gemma3:4b
```

## Návod ke spuštění

1. Nastavit venv a nainstalovat požadované balíčky:
```
cd MeetingMinutes.Client/python
python -m venv .venv
.venv/Scripts/activate
pip install -r requirements.txt
```

2. Stáhnout `canary-1b-v2.nemo` model na stránce `https://huggingface.co/nvidia/canary-1b-v2/tree/main` a umístit do složky `MeetingMinutes.Client\python\Models`.

3. V terminálu ve složce `MeetingMinutes.Client` spusťit příkaz:
```
dotnet publish -c Release -r win-x64 --self-contained true
```

4. Zkopírovat python/ složku vedle exe do publish složky: `bin/Release/net10.0-windows/win-x64/publish/`

5. Nyní je aplikace spustitelná, `publish/` výslednou složku lze zabalit do zipu a poslat uživatelům
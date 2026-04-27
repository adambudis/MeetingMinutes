using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MeetingMinutes.Services;

public class LocalPythonTranscriptionService : ITranscriptionService
{
    public async Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(
        string audioPath,
        string model,
        string? language,
        Action<string> onProgress,
        CancellationToken cancellationToken = default)
    {
        var (pythonExe, scriptPath) = FindPythonAndScript();
        onProgress($"Python: {pythonExe}");
        onProgress($"Script: {scriptPath}");

        var args = new StringBuilder($"\"{scriptPath}\" \"{audioPath}\" --model {model}");
        if (language != null)
            args.Append($" --language {language}");

        var psi = new ProcessStartInfo(pythonExe, args.ToString())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!
        };

        using var process = Process.Start(psi)
            ?? throw new Exception("Nepodařilo se spustit Python.");

        var stderrLines = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stderrLines.AppendLine(e.Data);
            onProgress(e.Data);
        };
        process.BeginErrorReadLine();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;

        if (process.ExitCode != 0)
        {
            var stderr = stderrLines.ToString();
            throw new Exception(stderr.Length > 0 ? stderr : $"Skript skončil s kódem {process.ExitCode}");
        }

        return ParseOutput(stdout);
    }

    private static (string pythonExe, string scriptPath) FindPythonAndScript()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

#if DEBUG
        var root = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\"));
        var python = Path.Combine(root, "python", ".venv", "Scripts", "python.exe");
        var script = Path.Combine(root, "python", "app.py");
        if (File.Exists(python) && File.Exists(script))
            return (python, script);
#endif

        var prodPython = Path.Combine(baseDir, "python", ".venv", "Scripts", "python.exe");
        var prodScript = Path.Combine(baseDir, "python", "app.py");
        if (File.Exists(prodPython) && File.Exists(prodScript))
            return (prodPython, prodScript);

        throw new Exception(
            $"Nelze najít python.exe nebo app.py.\n" +
            $"Hledáno v: {Path.Combine(baseDir, "python")}");
    }

    private static IReadOnlyList<TranscriptSegment> ParseOutput(string stdout)
    {
        // NeMo may prefix stdout with non-JSON noise — find our JSON payload
        var jsonStart = stdout.IndexOf("{\"segments\"");
        if (jsonStart < 0) jsonStart = stdout.IndexOf("{\"error\"");
        if (jsonStart > 0) stdout = stdout[jsonStart..];

        using var doc  = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorProp))
            throw new InvalidOperationException($"Chyba Pythonu: {errorProp.GetString()}");

        if (!root.TryGetProperty("segments", out var segments))
            throw new InvalidOperationException("Neočekávaný formát výstupu Pythonu (chybí pole 'segments').");

        var result = new List<TranscriptSegment>();
        foreach (var seg in segments.EnumerateArray())
        {
            var start = seg.GetProperty("start").GetDouble();
            var text = seg.GetProperty("text").GetString()?.Trim() ?? string.Empty;
            var speaker = seg.TryGetProperty("speaker", out var sp) ? sp.GetString() ?? "???" : "???";
            result.Add(new TranscriptSegment(start, speaker, text));
        }
        return result;
    }
}

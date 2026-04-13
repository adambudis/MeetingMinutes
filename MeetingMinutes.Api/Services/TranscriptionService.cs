using System.Diagnostics;
using System.Text.Json;

namespace MeetingMinutes.Api.Services
{
    public class TranscriptionService
    {
        private static readonly HashSet<string> AllowedExtensions = [".wav", ".mp3"];

        private readonly string _pythonDir;
        private readonly string _pythonExe;

        public TranscriptionService(IWebHostEnvironment env)
        {
            _pythonDir = Path.Combine(env.ContentRootPath, "python");
            _pythonExe = Path.Combine(_pythonDir, ".venv", "Scripts", "python.exe");
        }

        public static bool IsAllowedExtension(string ext) =>
            AllowedExtensions.Contains(ext.ToLowerInvariant());

        public async Task<JsonElement> TranscribeAsync(string audioPath, CancellationToken cancellationToken)
        {
            var appScript = Path.Combine(_pythonDir, "app.py");
            var psi = new ProcessStartInfo
            {
                FileName = _pythonExe,
                Arguments = $"\"{appScript}\" \"{audioPath}\" --backend parakeet",
                WorkingDirectory = _pythonDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start Python process.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
                throw new TranscriptionException("Transcription failed.", stderr);

            // Some libraries print non-JSON lines to stdout; the JSON result is always last.
            var jsonLine = stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault(l => l.StartsWith('{') || l.StartsWith('['));

            if (jsonLine is null)
                throw new TranscriptionException("No JSON output from transcription.", stdout);

            using var doc = JsonDocument.Parse(jsonLine);
            return doc.RootElement.Clone();
        }
    }

    public class TranscriptionException(string message, string detail)
        : Exception(message)
    {
        public string Detail { get; } = detail;
    }
}

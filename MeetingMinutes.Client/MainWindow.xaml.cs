using NAudio.Wave;
using OllamaSharp;
using OllamaSharp.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace MeetingMinutes
{
    public partial class MainWindow : Window
    {
        private WaveInEvent? recorder;
        private WaveFileWriter? writer;
        private string tempRecordingPath = string.Empty;
        private bool _isPaused = false;

        private readonly System.Diagnostics.Stopwatch _recordingStopwatch = new();
        private readonly System.Windows.Threading.DispatcherTimer _uiTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        private string fullTranscript = string.Empty;
        private const string OllamaModel = "gemma3:4b";
        private readonly OllamaApiClient ollamaClient = new(new Uri("http://localhost:11434"));

        public MainWindow()
        {
            InitializeComponent();
            _uiTimer.Tick += (_, _) =>
            {
                var e = _recordingStopwatch.Elapsed;
                TimerLabel.Text = $"{(int)e.TotalMinutes:D2}:{e.Seconds:D2}";
            };
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            tempRecordingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
            TranscriptBox.Clear();
            TranscriptBox.AppendText("Nahrávám...\n");
            SummarizeButton.IsEnabled = false;

            recorder = new WaveInEvent
            {
                WaveFormat = new WaveFormat(rate: 16000, bits: 16, channels: 1)
            };

            recorder.DataAvailable += Recorder_DataAvailable;
            recorder.RecordingStopped += Recorder_RecordingStopped;

            try
            {
                writer = new WaveFileWriter(tempRecordingPath, recorder.WaveFormat);
                recorder.StartRecording();

                _isPaused = false;
                _recordingStopwatch.Restart();
                _uiTimer.Start();
                StartButton.IsEnabled = false;
                PauseButton.IsEnabled = true;
                StopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při spuštění nahrávání:\n{ex.Message}");
            }
        }

        private void Recorder_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_isPaused) return;

            writer?.Write(e.Buffer, 0, e.BytesRecorded);

            // Vizualizace hlasitosti
            double maxLevel = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                maxLevel = Math.Max(maxLevel, Math.Abs(sample));
            }
            Dispatcher.Invoke(() => VolumeBar.Value = maxLevel / short.MaxValue);
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            var icon = (MaterialDesignThemes.Wpf.PackIcon)PauseButton.Content;
            if (_isPaused)
            {
                _recordingStopwatch.Stop();
                _uiTimer.Stop();
                icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Play;
                PauseButton.ToolTip = "Pokračovat v nahrávání";
                VolumeBar.Value = 0;
            }
            else
            {
                _recordingStopwatch.Start();
                _uiTimer.Start();
                icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Pause;
                PauseButton.ToolTip = "Pozastavit nahrávání";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            recorder?.StopRecording();
        }

        private void Recorder_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            writer?.Dispose();
            writer = null;
            recorder?.Dispose();
            recorder = null;

            var temp = tempRecordingPath;

            Dispatcher.Invoke(() =>
            {
                _recordingStopwatch.Reset();
                _uiTimer.Stop();
                TimerLabel.Text = "00:00";
                StartButton.IsEnabled = true;
                PauseButton.IsEnabled = false;
                _isPaused = false;
                ((MaterialDesignThemes.Wpf.PackIcon)PauseButton.Content).Kind = MaterialDesignThemes.Wpf.PackIconKind.Pause;
                PauseButton.ToolTip = "Pozastavit nahrávání";
                StopButton.IsEnabled = false;

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Uložit nahrávku",
                    Filter = "WAV soubor (*.wav)|*.wav",
                    FileName = DateTime.Now.ToString("yyyy-MM-dd") + ".wav"
                };

                if (saveDialog.ShowDialog() != true)
                {
                    File.Delete(temp);
                    ImportButton.IsEnabled = true;
                    TranscriptBox.Clear();
                    return;
                }

                File.Move(temp, saveDialog.FileName, overwrite: true);
                var savedPath = saveDialog.FileName;

                ImportButton.IsEnabled = false;
                TranscriptBox.Clear();
                TranscriptBox.AppendText("Nahrávání dokončeno. Spouštím přepis a rozpoznávání mluvčích...\n");

                Task.Run(async () => await RunTranscriptionAsync(savedPath));
            });
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Vyber audio soubor",
                Filter = "Audio/Video soubory (*.wav;*.mp3;*.m4a;*.mp4)|*.wav;*.mp3;*.m4a;*.mp4|Všechny soubory (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            var filePath = dialog.FileName;

            TranscriptBox.Clear();
            TranscriptBox.AppendText("Spouštím přepis a rozpoznávání mluvčích...\n");
            ImportButton.IsEnabled = false;
            SummarizeButton.IsEnabled = false;

            Task.Run(async () =>
            {
                var wavPath = filePath;
                string? tempWav = null;
                try
                {
                    if (!filePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    {
                        tempWav = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
                        ConvertToWav(filePath, tempWav);
                        wavPath = tempWav;
                    }
                    await RunTranscriptionAsync(wavPath);
                }
                finally
                {
                    if (tempWav != null && File.Exists(tempWav))
                        File.Delete(tempWav);
                }
            });
        }

        private static void ConvertToWav(string inputPath, string outputPath)
        {
            using var reader = new AudioFileReader(inputPath);
            using var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 16, 1));
            WaveFileWriter.CreateWaveFile(outputPath, resampler);
        }

        private static (string pythonExe, string scriptPath) FindPythonAndScript()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                var python = Path.Combine(dir, "python", ".venv", "Scripts", "python.exe");
                var script = Path.Combine(dir, "python", "app.py");
                if (File.Exists(python) && File.Exists(script))
                    return (python, script);
                dir = Path.GetDirectoryName(dir);
            }
            throw new Exception("Nelze najít python.exe nebo app.py (očekáváno ve složce python/.venv/ vedle exe).");
        }

        private async Task RunTranscriptionAsync(string audioPath)
        {
            var (pythonExe, scriptPath) = FindPythonAndScript();

            var language = Dispatcher.Invoke(() =>
                LanguageSelector.SelectedIndex == 1 ? "en" : "cs");

            var args = new StringBuilder();
            args.Append($"\"{scriptPath}\"");
            args.Append($" \"{audioPath}\"");
            args.Append($" --model canary");
            args.Append($" --language {language}");

            try
            {
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

                // Stream stderr live to TranscriptBox as progress updates
                var stderrLines = new System.Text.StringBuilder();
                process.ErrorDataReceived += (_, args) =>
                {
                    if (args.Data == null) return;
                    stderrLines.AppendLine(args.Data);
                    Dispatcher.Invoke(() =>
                    {
                        TranscriptBox.AppendText(args.Data + "\n");
                        TranscriptBox.ScrollToEnd();
                    });
                };
                process.BeginErrorReadLine();

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var stdout = await stdoutTask;
                var stderr = stderrLines.ToString();

                if (process.ExitCode != 0)
                    throw new Exception(stderr.Length > 0 ? stderr : $"Skript skončil s kódem {process.ExitCode}");

                // NeMo may print non-JSON content to stdout — find our specific JSON
                var jsonStart = stdout.IndexOf("{\"segments\"");
                if (jsonStart < 0) jsonStart = stdout.IndexOf("{\"error\"");
                if (jsonStart > 0) stdout = stdout[jsonStart..];

                var transcript = ParseTranscriptJson(stdout);

                Dispatcher.Invoke(() =>
                {
                    TranscriptBox.Clear();
                    TranscriptBox.AppendText(transcript);
                    TranscriptBox.ScrollToEnd();
                    fullTranscript = transcript;
                    SummarizeButton.IsEnabled = true;
                    ImportButton.IsEnabled = true;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    TranscriptBox.AppendText($"\n[Chyba: {ex.Message}]\n");
                    ImportButton.IsEnabled = true;
                });
            }
        }

        private static string ParseTranscriptJson(string json)
        {
            var sb = new StringBuilder();
            using var doc = JsonDocument.Parse(json);
            var segments = doc.RootElement.GetProperty("segments");

            foreach (var segment in segments.EnumerateArray())
            {
                var start = segment.GetProperty("start").GetDouble();
                var text = segment.GetProperty("text").GetString()?.Trim() ?? string.Empty;
                var speaker = segment.TryGetProperty("speaker", out var sp)
                    ? sp.GetString() ?? "???"
                    : "???";

                var ts = TimeSpan.FromSeconds(start);
                sb.AppendLine($"[{ts:mm\\:ss}] {speaker}: {text}");
            }

            return sb.ToString();
        }
         
        private async void SummarizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(fullTranscript))
            {
                MessageBox.Show("Není k dispozici žádný přepis.");
                return;
            }

            SummarizeButton.IsEnabled = false;
            SummaryBox.Clear();
            SummaryBox.AppendText("Generuji shrnutí...\n\n");

            var structurePrompt = string.IsNullOrWhiteSpace(PromptBox.Text)
                ? "Vytvoř shrnutí schůzky."
                : PromptBox.Text;

            const string systemPrompt =        
            @"Jsi asistent pro shrnutí přepisů schůzek.
                Pravidla:
                - odpovídej pouze česky
                - používej pouze informace z přepisu
                - nic si nevymýšlej
                - pokud informace chybí napiš 'není uvedeno'
                - pokud v přepisu nejsou rozhodnutí nebo úkoly, napiš to výslovně
                - nevytvářej nové osoby, role, úkoly ani závěry
                - shrnutí musí být stručné a věcné";

            var userMessage = $"{structurePrompt}\n\nTranskript:\n{fullTranscript}";
             
            try
            {
                ollamaClient.SelectedModel = OllamaModel;

                var request = new GenerateRequest
                {
                    Model = OllamaModel,
                    Prompt = userMessage, 
                    System = systemPrompt,
                    Stream = true,
                    Options = new RequestOptions { Temperature = 0.3f },
                };

                await foreach (var stream in ollamaClient.GenerateAsync(request))
                {
                    if (!string.IsNullOrEmpty(stream?.Response))
                    {
                        SummaryBox.AppendText(stream.Response);
                        SummaryBox.ScrollToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                SummaryBox.AppendText($"\n[Chyba: {ex.Message}]");
            }
            finally
            {
                SummarizeButton.IsEnabled = true;
            }
        }
    }
}

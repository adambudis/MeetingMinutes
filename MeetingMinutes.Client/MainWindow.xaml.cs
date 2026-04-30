using NAudio.Wave;
using MeetingMinutes.Dialogs;
using MeetingMinutes.Services;
using MeetingMinutes.Settings;
using MeetingMinutes.ViewModels;
using OllamaSharp.Models.Chat;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Threading;

namespace MeetingMinutes
{
    public partial class MainWindow : Window
    {
        private WaveInEvent? recorder;
        private WaveFileWriter? writer;
        private string tempRecordingPath = string.Empty;
        private bool _isPaused = false;

        private readonly Stopwatch _recordingStopwatch = new();
        private readonly DispatcherTimer _uiTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        private string fullTranscript = string.Empty;
        private string? _pendingWavPath;
        private string? _pendingTempWav;
        private LlmMessage? _systemMessage;
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
        private UserSettingsData _userSettings = UserSettings.Load();
        private readonly ITranscriptionService _transcriptionService = new LocalPythonTranscriptionService();
        private readonly ILlmService _llmService = new OllamaLlmService();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _uiTimer.Tick += (_, _) =>
            {
                var e = _recordingStopwatch.Elapsed;
                TimerLabel.Text = $"{(int)e.TotalMinutes:D2}:{e.Seconds:D2}";
            };
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            tempRecordingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");

            if (_pendingTempWav != null && File.Exists(_pendingTempWav))
                File.Delete(_pendingTempWav);
            _pendingTempWav = null;
            _pendingWavPath = null;
            TranscribeButton.IsEnabled = false;

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
                _pendingWavPath = savedPath;

                ShowPendingTranscriptionInfo($"Nahrávání dokončeno: {Path.GetFileName(savedPath)}");
                ImportButton.IsEnabled = false;
                TranscribeButton.IsEnabled = true;
            });
        }

        private void PromptBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SummarizeButton.IsEnabled)
            {
                e.Handled = true;
                SummarizeButton_Click(sender, e);
            }
        }

        private void ClearChatButton_Click(object sender, RoutedEventArgs e)
        {
            ChatMessages.Clear();
            ClearChatButton.IsEnabled = false;
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e) =>
            await ShowSettingsDialogAsync(new SettingsDialogView(_userSettings));

        private async void TranscriptionSettingsButton_Click(object sender, RoutedEventArgs e) =>
            await ShowSettingsDialogAsync(new TranscriptionSettingsDialogView(_userSettings));

        private async Task ShowSettingsDialogAsync(object dialog)
        {
            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
            if (result is UserSettingsData updated)
            {
                _userSettings = updated;
                UserSettings.Save(updated);
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Vyber audio soubor",
                Filter = "Audio/Video soubory (*.wav;*.mp3;*.m4a;*.mp4)|*.wav;*.mp3;*.m4a;*.mp4|Všechny soubory (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            var filePath = dialog.FileName;

            if (_pendingTempWav != null && File.Exists(_pendingTempWav))
                File.Delete(_pendingTempWav);
            _pendingTempWav = null;
            _pendingWavPath = null;

            TranscribeButton.IsEnabled = false;
            SummarizeButton.IsEnabled = false;
            TranscriptBox.Clear();
            TranscriptBox.AppendText("Konvertuji soubor...\n");

            try
            {
                string wavPath = filePath;
                await Task.Run(() =>
                {
                    if (!filePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    {
                        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
                        ConvertToWav(filePath, tmp);
                        wavPath = tmp;
                        _pendingTempWav = tmp;
                    }
                });

                _pendingWavPath = wavPath;
                ShowPendingTranscriptionInfo($"Importováno: {Path.GetFileName(filePath)}");
                TranscribeButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                TranscriptBox.AppendText($"\n[Chyba: {ex.Message}]\n");
            }
        }

        private async void TranscribeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingWavPath == null) return;

            var wavPath = _pendingWavPath;
            var tempWav = _pendingTempWav;
            _pendingWavPath = null;
            _pendingTempWav = null;

            TranscribeButton.IsEnabled = false;
            ImportButton.IsEnabled = false;

            try
            {
                await RunTranscriptionAsync(wavPath);
            }
            finally
            {
                if (tempWav != null && File.Exists(tempWav))
                    File.Delete(tempWav);
                ImportButton.IsEnabled = true;
            }
        }

        private static void ConvertToWav(string inputPath, string outputPath)
        {
            using var reader = new AudioFileReader(inputPath);
            using var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 16, 1));
            WaveFileWriter.CreateWaveFile(outputPath, resampler);
        }

        private void ShowPendingTranscriptionInfo(string headerLine)
        {
            var model = _userSettings.TranscriptionModel;
            var modelInfo = model == "canary"
                ? $"Model: {model} | Jazyk: {_userSettings.TranscriptionLanguage}"
                : $"Model: {model}";
            TranscriptBox.Clear();
            TranscriptBox.AppendText($"{headerLine}\n{modelInfo}\n\nNastavení přepisu změníš kliknutím na ⚙ vlevo dole.");
        }

        private async Task RunTranscriptionAsync(string audioPath)
        {
            var model = _userSettings.TranscriptionModel;
            var language = model == "canary" ? _userSettings.TranscriptionLanguage : null;

            try
            {
                var segments = await _transcriptionService.TranscribeAsync(
                    audioPath, model, language,
                    onProgress: line => Dispatcher.Invoke(() =>
                    {
                        TranscriptBox.AppendText(line + "\n");
                        TranscriptBox.ScrollToEnd();
                    }));

                var transcript = FormatTranscript(segments);

                Dispatcher.Invoke(() =>
                {
                    TranscriptBox.Clear();
                    TranscriptBox.AppendText(transcript);
                    TranscriptBox.ScrollToEnd();
                    fullTranscript = transcript;
                    _systemMessage = null;
                    ChatMessages.Clear();
                    ClearChatButton.IsEnabled = false;
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

        private static string FormatTranscript(IReadOnlyList<TranscriptSegment> segments)
        {
            var sb = new StringBuilder();
            foreach (var s in segments)
            {
                var ts = TimeSpan.FromSeconds(s.Start);
                sb.AppendLine($"[{ts:mm\\:ss}] {s.Speaker}: {s.Text}");
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

            var userText = string.IsNullOrWhiteSpace(PromptBox.Text)
                ? "Vytvoř shrnutí schůzky."
                : PromptBox.Text;

            SummarizeButton.IsEnabled = false;
            PromptBox.Clear();

            _systemMessage ??= new LlmMessage(ChatRole.System, $"{_userSettings.SystemPrompt}\n\nTranskript:\n{fullTranscript}");

            ChatMessages.Add(new ChatMessage(isUser: true, content: userText));
            ClearChatButton.IsEnabled = true;

            var messages = new List<LlmMessage> { _systemMessage };
            messages.AddRange(ChatMessages.Select(m => new LlmMessage(
                m.IsUser ? ChatRole.User : ChatRole.Assistant, m.Content)));

            var assistantMessage = new ChatMessage(isUser: false);
            ChatMessages.Add(assistantMessage);
            ChatScrollViewer.ScrollToEnd();

            try
            {
                await _llmService.CompleteAsync(
                    messages,
                    _userSettings.OllamaModel,
                    onChunk: chunk =>
                    {
                        assistantMessage.Content += chunk;
                        ChatScrollViewer.ScrollToEnd();
                    });
            }
            catch (Exception ex)
            {
                assistantMessage.Content = $"[Chyba: {ex.Message}]";
            }
            finally
            {
                SummarizeButton.IsEnabled = true;
            }
        }
    }
}

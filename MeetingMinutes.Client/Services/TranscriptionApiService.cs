using MeetingMinutes.Client.ViewModels;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MeetingMinutes.Services
{
    public class TranscriptionApiService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(60) };
        private readonly string _baseUrl;

        public TranscriptionApiService()
        {
            _baseUrl = AppConfig.ApiBaseUrl;
        }

        public async Task<TranscriptionSegment[]> TranscribeAsync(string audioPath, CancellationToken cancellationToken = default)
        {
            await using var stream = File.OpenRead(audioPath);
            var fileName = Path.GetFileName(audioPath);

            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            content.Add(fileContent, "file", fileName);

            var response = await _http.PostAsync($"{_baseUrl}/api/transcription", content, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string detail;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    detail = doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() ?? body
                           : doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() ?? body
                           : body;
                }
                catch
                {
                    detail = body;
                }
                throw new Exception($"Přepis selhal ({(int)response.StatusCode}): {detail}");
            }

            using var result = JsonDocument.Parse(body);
            var segments = result.RootElement.GetProperty("segments");
            return JsonSerializer.Deserialize<TranscriptionSegment[]>(segments.GetRawText())
                ?? [];
        }
    }
}

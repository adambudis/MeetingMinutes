using System.Net.Http;
using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace MeetingMinutes.Services;

public class OllamaLlmService : ILlmService
{
    private static readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://localhost:11434"),
        Timeout = Timeout.InfiniteTimeSpan
    };
    private readonly OllamaApiClient _client = new(_http);

    public async Task<string> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        Action<string> onChunk,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatRequest
        {
            Model = model,
            Messages = messages.Select(m => new Message(MapRole(m.Role), m.Content)).ToList(),
            Stream = true,
            Options = new RequestOptions { Temperature = 0.3f },
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _client.ChatAsync(request, cancellationToken))
        {
            var content = chunk?.Message?.Content;
            if (!string.IsNullOrEmpty(content))
            {
                onChunk(content);
                sb.Append(content);
            }
        }
        return sb.ToString();
    }

    private static ChatRole MapRole(LlmRole role) => role switch
    {
        LlmRole.System => ChatRole.System,
        LlmRole.Assistant => ChatRole.Assistant,
        _                 => ChatRole.User,
    };
}

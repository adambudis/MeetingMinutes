using OllamaSharp.Models.Chat;

namespace MeetingMinutes.Services;

public record LlmMessage(ChatRole Role, string Content);

public interface ILlmService
{
    Task<string> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        Action<string> onChunk,
        CancellationToken cancellationToken = default);
}

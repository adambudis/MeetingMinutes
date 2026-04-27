namespace MeetingMinutes.Services;

public enum LlmRole { System, User, Assistant }

public record LlmMessage(LlmRole Role, string Content);

public interface ILlmService
{
    Task<string> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        Action<string> onChunk,
        CancellationToken cancellationToken = default);
}

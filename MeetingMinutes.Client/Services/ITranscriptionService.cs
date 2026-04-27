namespace MeetingMinutes.Services;

public record TranscriptSegment(double Start, string Speaker, string Text);

public interface ITranscriptionService
{
    Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(
        string audioPath,
        string model,
        string? language,
        Action<string> onProgress,
        CancellationToken cancellationToken = default);
}

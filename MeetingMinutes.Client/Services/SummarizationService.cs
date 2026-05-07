using System.Text;
using OllamaSharp.Models.Chat;

namespace MeetingMinutes.Services;

public class SummarizationService(ILlmService llm) : ISummarizationService
{
    private const int CharsPerToken = 4;

    public async Task<string> SummarizeAsync(
        SummarizationRequest request,
        Action<int, int> onChunkStarted,
        Action<string> onToken,
        CancellationToken cancellationToken = default)
    {
        var chunks = ChunkTranscript(request.Transcript);
        string previousSummary = "";

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = new SummarizationChunk(chunks[i], i, chunks.Count, previousSummary);
            onChunkStarted(i + 1, chunks.Count);

            var systemPrompt = i == 0
                ? request.SystemPrompt
                : request.SystemPrompt + "\n\nPro tuto část: Sekci 'Účastníci' přenášej beze změny z dosavadního shrnutí — nové účastníky nepřidávej ani stávající neodebírej.";

            var messages = new List<LlmMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, chunk.UserContent),
            };

            previousSummary = await llm.CompleteAsync(
                messages, request.Model, onToken, cancellationToken);
        }

        return previousSummary;
    }

    public Task<string> ContinueAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        Action<string> onToken,
        CancellationToken cancellationToken = default) =>
        llm.CompleteAsync(messages, model, onToken, cancellationToken);

    private static List<string> ChunkTranscript(string transcript, int maxTokens = 4000)
    {
        int maxChars = maxTokens * CharsPerToken;
        var lines = transcript.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var line in lines)
        {
            if (current.Length > 0 && current.Length + line.Length + 1 > maxChars)
            {
                chunks.Add(current.ToString().TrimEnd());
                current.Clear();
            }
            current.AppendLine(line);
        }

        if (current.Length > 0)
            chunks.Add(current.ToString().TrimEnd());

        return chunks;
    }
}

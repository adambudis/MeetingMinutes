namespace MeetingMinutes.Services;

public record SummarizationRequest(string Transcript, string SystemPrompt, string Model);

public record SummarizationChunk(string Transcript, int Index, int Total, string PreviousSummary)
{
    public string UserContent => Index == 0
        ? $"Přepis schůzky (část {Index + 1}/{Total}):\n\n{Transcript}"
        : $"Dosavadní shrnutí:\n\n{PreviousSummary}\n\n" +
          $"Další část přepisu ({Index + 1}/{Total}):\n\n{Transcript}\n\n" +
          $"Vytvoř jedno aktualizované sloučené shrnutí ve stejné struktuře. " +
          $"Výstup musí být jediné kompletní shrnutí — nespisuj dvě shrnutí za sebou, " +
          $"vše slep do jednoho. Zachovej relevantní informace z dosavadního shrnutí a doplň nové.";
}

public interface ISummarizationService
{
    Task<string> SummarizeAsync(
        SummarizationRequest request,
        Action<int, int> onChunkStarted,
        Action<string> onToken,
        CancellationToken cancellationToken = default);

    Task<string> ContinueAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        Action<string> onToken,
        CancellationToken cancellationToken = default);
}

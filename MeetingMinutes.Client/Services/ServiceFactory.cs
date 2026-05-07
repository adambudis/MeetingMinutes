namespace MeetingMinutes.Services;

internal static class ServiceFactory
{
    public static ITranscriptionService CreateTranscriptionService() => new LocalPythonTranscriptionService();
    public static ISummarizationService CreateSummarizationService() => new SummarizationService(new OllamaLlmService());
}

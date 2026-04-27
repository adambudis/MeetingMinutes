using System.IO;
using System.Text.Json;

namespace MeetingMinutes.Settings;

public class UserSettingsData
{
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;
    public string OllamaModel { get; set; } = "gemma3:4b";
    public string TranscriptionModel { get; set; } = "canary";
    public string TranscriptionLanguage { get; set; } = "cs";

    public UserSettingsData Clone() => (UserSettingsData)MemberwiseClone();

    public const string DefaultSystemPrompt =
        """
        Jsi asistent pro shrnutí přepisů schůzek.

            VÝSTUP MUSÍ MÍT TUTO STRUKTURU:

            1) Témata:
            - ...

            2) Účastníci:
            - ...

            3) Konkrétní návrhy / opatření:
            - ...

            4) Rozhodnutí:
            - ...

            5) Úkoly:
            - ...

            PRAVIDLA:
            - odpovídej pouze česky
            - používej pouze informace z přepisu
            - nic si nevymýšlej
            - pokud informace chybí napiš "není uvedeno"
            - pokud v přepisu nejsou rozhodnutí nebo úkoly, napiš to výslovně
            - používej pouze jména, která jsou v přepisu
            - NEUVÁDĚJ žádné obecné hodnocení (např. "celkový dojem")
            - NEVYTVÁŘEJ nové osoby ani role
            - NEZOBECŇUJ – používej formulace odpovídající textu (např. „uvádí", „říká", „navrhuje")
            - pokud si nejsi jistý, napiš "není uvedeno"
            - neuváděj informace, které nelze přímo dohledat v textu
            - buď stručný a věcný
        """;
}

public static class UserSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MeetingMinutes", "user-settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static UserSettingsData Load()
    {
        if (!File.Exists(FilePath)) return new UserSettingsData();
        try
        {
            return JsonSerializer.Deserialize<UserSettingsData>(File.ReadAllText(FilePath), JsonOpts)
                   ?? new UserSettingsData();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserSettings] Failed to load: {ex.Message}");
            return new UserSettingsData();
        }
    }

    public static void Save(UserSettingsData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOpts));
    }
}

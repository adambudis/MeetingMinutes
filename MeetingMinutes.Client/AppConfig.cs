using System;
using System.IO;
using System.Text.Json;

namespace MeetingMinutes
{
    public static class AppConfig
    {
        private static readonly Lazy<Settings> _settings = new(() =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Konfigurační soubor nenalezen: {path}");

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return new Settings(
                ApiBaseUrl: doc.RootElement.GetProperty("ApiBaseUrl").GetString()
                    ?? throw new InvalidOperationException("ApiBaseUrl není nastaven v appsettings.json")
            );
        });

        public static string ApiBaseUrl => _settings.Value.ApiBaseUrl;

        private record Settings(string ApiBaseUrl);
    }
}

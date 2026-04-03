using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace openAIApps
{
    public class AppSettings
    {
        public string AppRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "openapi");
        public string LogsFolder { get; set; } = "logs";  // Relative to AppRoot
        public string SoundsFolder { get; set; } = "snds";
        public string ImagesFolder { get; set; } = "images";
        public string VideosFolder { get; set; } = "videos";
        public string ResponsesMarkdownTheme { get; set; } = "github.min.css";
        public string ResponsesPageTheme { get; set; } = "github-light-page.css";

        private static readonly string SettingsPath = Path.Combine(
    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
    "settings.json");

        public static AppSettings LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
    }
}

using System.IO;
using System.Text.Json;

namespace PlannamTypora.Services
{
    public static class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PlannamTypora", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonSerializer.Deserialize<AppSettings>(
                        File.ReadAllText(SettingsPath)) ?? new();
            }
            catch { }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath,
                    JsonSerializer.Serialize(settings,
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }

    public class AppSettings
    {
        public bool IsDarkTheme { get; set; } = true;
        public string ViewMode { get; set; } = "Split";
        public double FontSize { get; set; } = 14;
        public double SidebarWidth { get; set; } = 220;
        public bool SidebarVisible { get; set; } = true;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 750;
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public bool WindowMaximized { get; set; } = false;
        public bool ShowStatusBar { get; set; } = true;
        public bool AlwaysOnTop { get; set; } = false;
    }
}

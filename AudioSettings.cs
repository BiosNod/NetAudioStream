using System.Text.Json;
using System.IO;

namespace StreamingApplication
{
    public static class AudioSettings
    {
        private const string SettingsFile = "audio_settings.json";
        public static AudioSettingsData _settings = new();

        public class AudioSettingsData
        {
            public bool EnableCompression { get; set; } = false;
            public int Bitrate { get; set; } = 128;
            public int ServerLatency { get; set; } = 50;
            public int ClientLatency { get; set; } = 100;
        }

        static AudioSettings()
        {
            LoadSettings();
        }

        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    _settings = JsonSerializer.Deserialize<AudioSettingsData>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load audio settings ({SettingsFile}): {ex.Message}", Logger.LogLevel.Warning);
            }
        }

        public static void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings);
                File.WriteAllText(SettingsFile, json);
                Logger.Log($"Audio settings saved to: {SettingsFile}", Logger.LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save audio settings: {ex.Message}", Logger.LogLevel.Error);
            }
        }
    }
}
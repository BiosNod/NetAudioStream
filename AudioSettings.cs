using System.Text.Json;
using System.IO;

namespace StreamingApplication
{
    public static class AudioSettings
    {
        private const string SettingsFile = "audio_settings.json";
        private static AudioSettingsData _settings = new();

        public class AudioSettingsData
        {
            public bool EnableCompression { get; set; } = false;
            public int Bitrate { get; set; } = 128;
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
                Logger.Log($"Failed to load audio settings: {ex.Message}", Logger.LogLevel.Warning);
            }
        }

        private static void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save audio settings: {ex.Message}", Logger.LogLevel.Error);
            }
        }

        public static void SetCompression(bool enable, int bitrate = 128)
        {
            _settings.EnableCompression = enable;
            _settings.Bitrate = bitrate;
            SaveSettings();
        }

        public static (bool enabled, int bitrate) GetCompressionSettings()
        {
            return (_settings.EnableCompression, _settings.Bitrate);
        }
    }
}
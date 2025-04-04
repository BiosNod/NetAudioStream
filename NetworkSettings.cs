using System.Text.Json;
using System.IO;

namespace StreamingApplication
{
    public static class NetworkSettings
    {
        private const string SettingsFile = "network_settings.json";
        private static NetworkSettingsData _settings = new();

        public class NetworkSettingsData
        {
            public string LastServerIp { get; set; } = "0.0.0.0";
            public int LastServerPort { get; set; } = 49800;
            public string LastClientIp { get; set; } = "127.0.0.1";
            public int LastClientPort { get; set; } = 49800;
        }

        static NetworkSettings()
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
                    _settings = JsonSerializer.Deserialize<NetworkSettingsData>(json) ?? new();
                    Logger.Log("Network settings loaded successfully", Logger.LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load network settings: {ex.Message}", Logger.LogLevel.Warning);
                _settings = new NetworkSettingsData();
            }
        }

        private static void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings);
                File.WriteAllText(SettingsFile, json);
                Logger.Log("Network settings saved successfully", Logger.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save network settings: {ex.Message}", Logger.LogLevel.Error);
            }
        }

        public static void ShowCurrentSettings()
        {
            Console.WriteLine("\nCurrent Network Settings:");
            Console.WriteLine($"Server IP: {_settings.LastServerIp}");
            Console.WriteLine($"Server Port: {_settings.LastServerPort}");
            Console.WriteLine($"Client IP: {_settings.LastClientIp}");
            Console.WriteLine($"Client Port: {_settings.LastClientPort}");
            Console.WriteLine($"Config file: {Path.GetFullPath(SettingsFile)}");
        }

        public static void ResetSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    File.Delete(SettingsFile);
                    Logger.Log("Network settings reset to defaults", Logger.LogLevel.Info);
                }
                _settings = new NetworkSettingsData();
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to reset settings: {ex.Message}", Logger.LogLevel.Error);
            }
        }

        public static (string ip, int port) GetServerSettings()
        {
            Console.Write($"Enter IP [{_settings.LastServerIp}]: ");
            var ipInput = Console.ReadLine();

            var ip = string.IsNullOrWhiteSpace(ipInput)
                ? _settings.LastServerIp
                : ipInput.Trim();

            Console.Write($"Enter Port [{_settings.LastServerPort}]: ");
            var portInput = Console.ReadLine();

            int port = string.IsNullOrWhiteSpace(portInput)
                ? _settings.LastServerPort
                : int.Parse(portInput);

            if (ip != _settings.LastServerIp || port != _settings.LastServerPort)
            {
                _settings.LastServerIp = ip;
                _settings.LastServerPort = port;
                SaveSettings();
            }

            return (ip, port);
        }

        public static (string ip, int port) GetClientSettings()
        {
            Console.Write($"Enter Server IP [{_settings.LastClientIp}]: ");
            var ipInput = Console.ReadLine();

            var ip = string.IsNullOrWhiteSpace(ipInput)
                ? _settings.LastClientIp
                : ipInput.Trim();

            Console.Write($"Enter Server Port [{_settings.LastClientPort}]: ");
            var portInput = Console.ReadLine();

            int port = string.IsNullOrWhiteSpace(portInput)
                ? _settings.LastClientPort
                : int.Parse(portInput);

            if (ip != _settings.LastClientIp || port != _settings.LastClientPort)
            {
                _settings.LastClientIp = ip;
                _settings.LastClientPort = port;
                SaveSettings();
            }

            return (ip, port);
        }
    }
}
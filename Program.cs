using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Runtime.InteropServices;

namespace StreamingApplication
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.CancelKeyPress += (s, e) => Environment.Exit(0);

            while (true)
            {
                Console.Clear();
                Console.WriteLine("1. Start Streaming Server");
                Console.WriteLine("2. Connect to Streaming Server");
                Console.WriteLine("3. Test Playback Device");
                Console.WriteLine("4. Audio Settings");
                Console.WriteLine("5. Network Settings");
                Console.WriteLine("6. Show Audio Devices");
                Console.WriteLine($"7. Toggle Debug Logs (Currently: {(Logger.DebugEnabled ? "ON" : "OFF")})");
                Console.WriteLine("8. Exit");

                switch (Console.ReadLine())
                {
                    case "1":
                        ServerMode();
                        break;
                    case "2":
                        await ClientMode();
                        break;
                    case "3":
                        await TestPlaybackDevice();
                        break;
                    case "4":
                        AudioSettings();
                        break;
                    case "5":
                        NetworkSettings();
                        break;
                    case "6":
                        ShowAudioDevicesMenu();
                        break;
                    case "7":
                        Logger.DebugEnabled = !Logger.DebugEnabled;
                        break;
                    case "8":
                        return;
                }
            }
        }

        static void AudioSettings()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Audio Compression Settings:");
                Console.WriteLine($"1. Toggle Compression [{(StreamingApplication.AudioSettings._settings.EnableCompression ? "Enabled" : "Disabled")}]");
                Console.WriteLine($"2. Set bitrate (will be in use only with compression) [Current: {StreamingApplication.AudioSettings._settings.Bitrate} kbps]");
                Console.WriteLine($"3. Set server latency: {StreamingApplication.AudioSettings._settings.ServerLatency} ms");
                Console.WriteLine($"4. Set client latency: {StreamingApplication.AudioSettings._settings.ClientLatency} ms");
                Console.WriteLine($"5. Toggle Normalization [{(StreamingApplication.AudioSettings._settings.EnableVolumeNormalization ? "Enabled" : "Disabled")}]");
                Console.WriteLine($"6. Set Normalization Level [Current: {StreamingApplication.AudioSettings._settings.VolumeNormalizationLevel}%]");
                Console.WriteLine($"7. Back to Main Menu");

                switch (Console.ReadLine())
                {
                    case "1":
                        StreamingApplication.AudioSettings._settings.EnableCompression = !StreamingApplication.AudioSettings._settings.EnableCompression;
                        Console.WriteLine($"Compression {(StreamingApplication.AudioSettings._settings.EnableCompression ? "Enabled" : "Disabled")}");
                        StreamingApplication.AudioSettings.SaveSettings();
                        break;
                    case "2":
                        Console.Write("Enter new bitrate (32-320): ");
                        if (int.TryParse(Console.ReadLine(), out int newRate) && newRate >= 32 && newRate <= 320)
                        {
                            StreamingApplication.AudioSettings._settings.Bitrate = newRate;
                            Console.WriteLine($"Bitrate set to {StreamingApplication.AudioSettings._settings.Bitrate} kbps");
                            StreamingApplication.AudioSettings.SaveSettings();
                        }
                        else
                        {
                            Console.WriteLine("Invalid bitrate! Must be 32-320");
                            Console.ReadKey();
                        }
                        break;

                    case "3":
                        Console.Write("Enter a new server latency (32-1000), must be less than the client latency around ~50ms: ");
                        if (int.TryParse(Console.ReadLine(), out int newServerLatency) && newServerLatency >= 32 && newServerLatency <= 1000)
                        {
                            StreamingApplication.AudioSettings._settings.ServerLatency = newServerLatency;
                            Console.WriteLine($"ServerLatency set to {StreamingApplication.AudioSettings._settings.ServerLatency} ms");
                            StreamingApplication.AudioSettings.SaveSettings();
                        }
                        else
                        {
                            Console.WriteLine("Invalid server latency! Must be 32-1000");
                            Console.ReadKey();
                        }
                        break;
                    case "4":
                        Console.Write("Enter a new client latency (32-1000), must be more than the server latency around ~50ms: ");
                        if (int.TryParse(Console.ReadLine(), out int newClientLatency) && newClientLatency >= 32 && newClientLatency <= 1000)
                        {
                            StreamingApplication.AudioSettings._settings.ClientLatency = newClientLatency;
                            Console.WriteLine($"ServerLatency set to {StreamingApplication.AudioSettings._settings.ClientLatency} ms");
                            StreamingApplication.AudioSettings.SaveSettings();
                        }
                        else
                        {
                            Console.WriteLine("Invalid client latency! Must be 32-1000");
                            Console.ReadKey();
                        }
                        break;
                    
                    case "5":
                        StreamingApplication.AudioSettings._settings.EnableVolumeNormalization = !StreamingApplication.AudioSettings._settings.EnableVolumeNormalization;
                        Console.WriteLine($"Normalization {(StreamingApplication.AudioSettings._settings.EnableVolumeNormalization ? "Enabled" : "Disabled")}");
                        StreamingApplication.AudioSettings.SaveSettings();
                        break;
                    case "6":
                        Console.Write("Enter new normalization level (0-150%): ");
                        if (int.TryParse(Console.ReadLine(), out int level) && level >= 0 && level <= 150)
                        {
                            StreamingApplication.AudioSettings._settings.VolumeNormalizationLevel = level;
                            StreamingApplication.AudioSettings.SaveSettings();
                            Console.WriteLine($"Normalization level set to {level}%");
                        }
                        else Console.WriteLine("Invalid value! Use 0-150");
                        break;
                    case "7":
                        return;
                }
            }
        }

        static void NetworkSettings()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Network Settings Management:");
                Console.WriteLine("1. View Current Settings");
                Console.WriteLine("2. Toggle UPnP (Current: " +
                    (StreamingApplication.NetworkSettings.IsUPnPEnabled() ? "Enabled" : "Disabled") + ")");
                Console.WriteLine("3. Reset to Defaults");
                Console.WriteLine("4. Back to Main Menu");

                switch (Console.ReadLine())
                {
                    case "1":
                        StreamingApplication.NetworkSettings.ShowCurrentSettings();
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();
                        break;
                    case "2":
                        StreamingApplication.NetworkSettings.ToggleUPnP();
                        Console.WriteLine($"\nUPnP is now {(StreamingApplication.NetworkSettings.IsUPnPEnabled() ? "Enabled" : "Disabled")}");
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                        break;
                    case "3":
                        Console.WriteLine("\nAre you sure you want to reset network settings? (y/n)");
                        if (Console.ReadLine()?.ToLower() == "y")
                        {
                            StreamingApplication.NetworkSettings.ResetSettings();
                            Console.WriteLine("Settings have been reset to defaults.");
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey();
                        }
                        break;
                    case "4":
                        return;
                }
            }
        }

        private static void ShowAudioDevicesMenu()
        {
            Console.Clear();
            Console.WriteLine("Audio Devices Information:");
            Console.WriteLine("1. Playback Devices");
            Console.WriteLine("2. Recording Devices");
            Console.WriteLine("3. Back to Main Menu");

            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    Console.WriteLine("\n[Playback Devices via WaveOut]");
                    AudioDeviceSelector.ListPlaybackDevicesWave();

                    Console.WriteLine("\n[Playback Devices via MMDevice]");
                    AudioDeviceSelector.ListMMDevices(DataFlow.Render);
                    break;

                case "2":
                    Console.WriteLine("\n[Recording Devices via MMDevice]");
                    AudioDeviceSelector.ListMMDevices(DataFlow.Capture);
                    break;
            }

            if (choice != "3")
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }

        static async Task TestPlaybackDevice()
        {
            try
            {
                int deviceIndex = AudioDeviceSelector.SelectPlaybackDeviceWaveOut();

                Console.Write($"Enter MP3 file path [test.mp3]: ");
                string testFileInput = Console.ReadLine();

                string testFile = string.IsNullOrWhiteSpace(testFileInput) ? "test.mp3" : testFileInput;

                // Список возможных путей для поиска файла
                var possiblePaths = new List<string>
                {
                    testFile,                            // Текущая директория
                    Path.Combine("..", testFile),        // Родительская директория
                    Path.Combine("../..", testFile),     // Два уровня выше
                    Path.Combine("../../..", testFile),  // Три уровня выше
                    Path.Combine("AudioFiles", testFile) // Папка AudioFiles
                };

                string? foundPath = possiblePaths.FirstOrDefault(File.Exists);

                if (foundPath == null)
                {
                    throw new Exception($"Test file '{testFile}' not found in:\n" +
                                       string.Join("\n", possiblePaths.Select(p => $" - {Path.GetFullPath(p)}")));
                }

                using var reader = new AudioFileReader(foundPath);
                using var waveOut = new WaveOutEvent
                {
                    DeviceNumber = deviceIndex
                };

                waveOut.Init(reader);
                waveOut.Play();

                var caps = new AudioDeviceSelector.WAVEOUTCAPS();
                AudioDeviceSelector.waveOutGetDevCaps(deviceIndex, ref caps, Marshal.SizeOf(caps));
                Console.WriteLine($"\nTesting: {caps.szPname} (Index: {deviceIndex})");
                Console.WriteLine($"Playing file: {Path.GetFullPath(foundPath)}");
                Console.WriteLine("\nPress Q to stop playback...");

                // Ожидаем завершения воспроизведения или нажатия Q
                while (waveOut.PlaybackState == PlaybackState.Playing)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                    {
                        waveOut.Stop();
                        Console.WriteLine("Playback stopped by user.");
                        break;
                    }
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Test error: {ex.Message}", Logger.LogLevel.Error);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        // Program.cs (обновление ServerMode)
        static void ServerMode()
        {
            try
            {
                Console.WriteLine("Select source:");
                Console.WriteLine("1. System Audio");
                Console.WriteLine("2. Microphone");
                Console.WriteLine("3. Application Process");
                var choice = Console.ReadLine();

                MMDevice device;
                DataFlow flow = DataFlow.Render;
                uint processId = 0;

                var enumerator = new MMDeviceEnumerator();

                if (choice == "3")
                {
                    processId = AudioDeviceSelector.SelectProcess();
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
                }
                else if (choice == "2")
                {
                    device = AudioDeviceSelector.SelectRecordingDeviceMMD();
                    flow = DataFlow.Capture;
                }
                else
                {
                    device = AudioDeviceSelector.SelectPlaybackDeviceMMD();
                }

                var (ip, port) = StreamingApplication.NetworkSettings.GetServerSettings();
                using var server = new AudioStreamingServer(ip, port, device, flow, processId);
                server.Start();

                Console.WriteLine("Server started. Press Q to stop...");
                while (Console.ReadKey(true).Key != ConsoleKey.Q) { }
                server.Stop();
            }
            catch (Exception ex)
            {
                Logger.Log($"Server error: {ex.Message}", Logger.LogLevel.Error);
                Console.ReadKey();
            }
        }

        static async Task ClientMode()
        {
            try
            {
                Console.Clear();
                var (ip, port) = StreamingApplication.NetworkSettings.GetClientSettings();
                int outputDevice = AudioDeviceSelector.SelectPlaybackDeviceWaveOut();

                using var client = new AudioStreamingClient();

                void ShowClientControls()
                {
                    Console.WriteLine("\nClient controls:");
                    Console.WriteLine("Q - Stop playback and disconnect");
                    Console.WriteLine("V - Adjust volume during playback\n");
                }

                // Добавляем обработчики событий
                client.OnConnected += ShowClientControls;

                client.OnDisconnected += reason =>
                    Logger.Log($"Disconnected: {reason}", Logger.LogLevel.Warning);

                client.OnReconnecting += attempt =>
                    Logger.Log($"Reconnection attempt {attempt}", Logger.LogLevel.Info);

                // Запускаем подключение
                await client.ConnectAsync(ip, port, outputDevice);

                // Основной цикл ожидания
                while (true)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        client.Disconnect();
                        Console.WriteLine("\nDisconnecting...");
                        await Task.Delay(500);
                        break;
                    }
                    else if (key.Key == ConsoleKey.V)
                    {
                        client.ShowVolumeControlMenu();
                    }
                    else
                        ShowClientControls();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Client error: {ex.Message}", Logger.LogLevel.Error);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
    }
}
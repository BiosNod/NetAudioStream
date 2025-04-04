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
                Console.WriteLine("4. Exit");
                Console.WriteLine($"5. Toggle Debug Logs (Currently: {(Logger.DebugEnabled ? "ON" : "OFF")})");
                Console.WriteLine("6. Show Audio Devices");
                Console.WriteLine("7. Manage Network Settings");

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
                        return;
                    case "5":
                        Logger.DebugEnabled = !Logger.DebugEnabled;
                        break;
                    case "6":
                        ShowAudioDevicesMenu();
                        break;
                    case "7":
                        ShowNetworkSettingsMenu();
                        break;
                }
            }
        }

        static void ShowNetworkSettingsMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Network Settings Management:");
                Console.WriteLine("1. View Current Settings");
                Console.WriteLine("2. Toggle UPnP (Current: " +
                    (NetworkSettings.IsUPnPEnabled() ? "Enabled" : "Disabled") + ")");
                Console.WriteLine("3. Reset to Defaults");
                Console.WriteLine("4. Back to Main Menu");

                switch (Console.ReadLine())
                {
                    case "1":
                        NetworkSettings.ShowCurrentSettings();
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();
                        break;
                    case "2":
                        NetworkSettings.ToggleUPnP();
                        Console.WriteLine($"\nUPnP is now {(NetworkSettings.IsUPnPEnabled() ? "Enabled" : "Disabled")}");
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                        break;
                    case "3":
                        Console.WriteLine("\nAre you sure you want to reset network settings? (y/n)");
                        if (Console.ReadLine()?.ToLower() == "y")
                        {
                            NetworkSettings.ResetSettings();
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

                var (ip, port) = NetworkSettings.GetServerSettings();
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
                var (ip, port) = NetworkSettings.GetClientSettings();
                var outputDevice = AudioDeviceSelector.SelectPlaybackDeviceWaveOut();

                using var client = new AudioStreamingClient();
                await client.ConnectAsync(ip, port, outputDevice);

                Console.WriteLine("Client started. Press Q to stop...");
                while (Console.ReadKey(true).Key != ConsoleKey.Q) { }
                client.Disconnect();
            }
            catch (Exception ex)
            {
                Logger.Log($"Client error: {ex.Message}", Logger.LogLevel.Error);
                Console.ReadKey();
            }
        }
    }
}
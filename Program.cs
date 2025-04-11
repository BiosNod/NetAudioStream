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
                        AudioMainSettings();
                        break;
                    case "5":
                        NetworkMainSettings();
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

        static void AudioMainSettings()
        {
            while (true)
            {
                Console.WriteLine("Audio Compression Settings:");
                Console.WriteLine($"1. Toggle Compression (Fastest GZIP reduce 3 times) [{(AudioSettings._settings.EnableCompression ? "Enabled" : "Disabled")}]");
                Console.WriteLine($"2. Set server latency: {AudioSettings._settings.ServerLatency} ms");
                Console.WriteLine($"3. Set client latency: {AudioSettings._settings.ClientLatency} ms");
                Console.WriteLine($"4. Toggle volume control [{(AudioSettings._settings.EnableVolumeControl ? "Enabled" : "Disabled")}]");
                Console.WriteLine($"5. Set volume level [Current: {AudioSettings._settings.VolumeLevel}%]");
                Console.WriteLine($"6. Toggle volume normalization [{(AudioSettings._settings.EnableVolumeNormalization ? "Enabled" : "Disabled")}]");
                Console.WriteLine($"7. Cancel (skip setup)");

                switch (Console.ReadLine())
                {
                    case "1":
                        AudioSettings._settings.EnableCompression = !AudioSettings._settings.EnableCompression;
                        Console.WriteLine($"Compression {(AudioSettings._settings.EnableCompression ? "Enabled" : "Disabled")}");
                        AudioSettings.SaveSettings();
                        break;

                    case "2":
                        var latencyServerMin = 10;
                        var latencyServerMax = 1000;
                        Console.Write($"Enter a new server latency ({latencyServerMin}-{latencyServerMax}), must be less than the client latency around ~50ms, default is 10ms: ");
                        if (int.TryParse(Console.ReadLine(), out int newServerLatency) && newServerLatency >= latencyServerMin && newServerLatency <= latencyServerMax)
                        {
                            AudioSettings._settings.ServerLatency = newServerLatency;
                            Console.WriteLine($"ServerLatency set to {AudioSettings._settings.ServerLatency} ms");
                            AudioSettings.SaveSettings();
                        }
                        else
                        {
                            Console.WriteLine($"Invalid server latency! Must be 32-{latencyServerMax}");
                            Console.ReadKey();
                        }
                        break;

                    case "3":
                        var latencyClientMin = 10;
                        var latencyClientMax = 1000;
                        Console.Write($"Enter a new client latency ({latencyClientMin}-{latencyClientMax}), must be more than the server latency around ~50ms, default is 60ms: ");
                        if (int.TryParse(Console.ReadLine(), out int newClientLatency) && newClientLatency >= latencyClientMin && newClientLatency <= latencyClientMax)
                        {
                            AudioSettings._settings.ClientLatency = newClientLatency;
                            Console.WriteLine($"ServerLatency set to {AudioSettings._settings.ClientLatency} ms");
                            AudioSettings.SaveSettings();
                        }
                        else
                        {
                            Console.WriteLine($"Invalid client latency! Must be {latencyClientMin}-{latencyClientMax}");
                            Console.ReadKey();
                        }
                        break;
                    
                    case "4":
                        AudioSettings._settings.EnableVolumeControl = !AudioSettings._settings.EnableVolumeControl;
                        Console.WriteLine($"Volume control {(AudioSettings._settings.EnableVolumeControl ? "Enabled" : "Disabled")}");
                        AudioSettings.SaveSettings();
                        AudioUtils.ResetNormalization();
                        break;

                    case "5":
                        var volumeLevelMin = 0;
                        var volumeLevelMax = 10000;

                        Console.WriteLine("\n=== Volume Control ===");
                        Console.WriteLine($"Current volume (default is 100): {AudioSettings._settings.VolumeLevel}");
                        Console.WriteLine($"Enter volume ({volumeLevelMin}-{volumeLevelMax}) or 'd' to disable control:");
                        Console.Write("> ");
                        var input = Console.ReadLine()?.Trim().ToLower();

                        if (input.ToUpper() == "D")
                        {
                            AudioSettings._settings.EnableVolumeControl = false;
                            AudioSettings.SaveSettings();
                            AudioUtils.ResetNormalization();
                            Console.WriteLine("Volume control disabled\n");
                        }
                        else if (int.TryParse(input, out int vol) && vol >= volumeLevelMin && vol <= volumeLevelMax)
                        {
                            AudioSettings._settings.VolumeLevel = vol;
                            AudioSettings._settings.EnableVolumeControl = true;
                            AudioSettings.SaveSettings();
                            AudioUtils.ResetNormalization();
                            Console.WriteLine($"Volume set to {vol}%\n");
                        }
                        else
                        {
                            Console.WriteLine($"Invalid input! Use {volumeLevelMin}-{volumeLevelMax} or 'd', skip, you can try again using V\n");
                        }
                        break;

                    case "6":
                        AudioSettings._settings.EnableVolumeNormalization = !AudioSettings._settings.EnableVolumeNormalization;
                        Console.WriteLine($"Volume normalization {(AudioSettings._settings.EnableVolumeNormalization ? "Enabled" : "Disabled")}");
                        AudioSettings.SaveSettings();
                        AudioUtils.ResetNormalization();
                        break;

                    case "7":
                        return;
                }
            }
        }

        static void NetworkMainSettings()
        {
            while (true)
            {
                Console.WriteLine("Network Settings Management:");
                Console.WriteLine("1. View Current Settings");
                Console.WriteLine("2. Toggle UPnP (Current: " +
                    (NetworkSettings.IsUPnPEnabled() ? "Enabled" : "Disabled") + ")");
                Console.WriteLine("3. Reset to Defaults");
                Console.WriteLine("4. Cancel (skip setup)");

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
            Console.WriteLine("Audio Devices Information:");
            Console.WriteLine("1. Playback Devices");
            Console.WriteLine("2. Recording Devices");
            Console.WriteLine("3. Cancel (skip setup)");

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
                string processName = "";

                var enumerator = new MMDeviceEnumerator();

                if (choice == "3")
                {
                    // Получаем полный путь к текущему EXE
                    string currentExePath = Environment.ProcessPath;
                    string currentExeDirectory = Path.GetDirectoryName(currentExePath);
                    string requiredExePath = Path.Combine(currentExeDirectory, ProcessAudioCapturer.ApplicationLoopbackPath);

                    // Проверяем наличие доп. модуля
                    if (!File.Exists(requiredExePath))
                    {
                        Console.WriteLine($"\n=== ERROR: {ProcessAudioCapturer.ApplicationLoopbackPath} not found! ===");
                        Console.WriteLine($"Current EXE location:\n    {currentExePath}"); // Показываем путь к текущему EXE
                        Console.WriteLine("\nRequired steps:");
                        Console.WriteLine("1. Download from: https://github.com/BiosNod/ApplicationLoopback");
                        Console.WriteLine("2. Open ApplicationLoopback.sln in Visual Studio");
                        Console.WriteLine("3. Build Release configuration");
                        Console.WriteLine($"4. Copy {ProcessAudioCapturer.ApplicationLoopbackPath} to this location:\n    {currentExeDirectory}");
                        Console.WriteLine("\n\nPress any key to return...");
                        Console.ReadKey();
                        return;
                    }

                    (processId, processName) = AudioDeviceSelector.SelectProcess();
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
                }
                else if (choice == "2")
                {
                    device = AudioDeviceSelector.SelectRecordingDeviceMMD();
                    flow = DataFlow.Capture;
                }
                else if ( choice == "1")
                {
                    device = AudioDeviceSelector.SelectPlaybackDeviceMMD();
                }
                else
                    throw new Exception("Wrong number");

                var (ip, port) = NetworkSettings.GetServerSettings();
                using var server = new AudioStreamingServer(ip, port, device, flow, processId, processName);
                server.Start();

                void ShowControls()
                {
                    Console.WriteLine("\nServer controls:");
                    Console.WriteLine("Q - Stop stream");
                    Console.WriteLine("A - Adjust audio during playback");
                    Console.WriteLine("N - Adjust network during playback");
                    Console.WriteLine("D - Enable/Disable debug logs\n");
                }

                ShowControls();

                // Основной цикл ожидания
                while (true)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        server.Stop();
                        Console.WriteLine("\nDisconnecting...");
                        break;
                    }
                    else if (key.Key == ConsoleKey.A)
                    {
                        AudioMainSettings();
                        Console.WriteLine("\nContinue streaming...");
                    }
                    else if (key.Key == ConsoleKey.N)
                    {
                        NetworkMainSettings();
                        Console.WriteLine("\nContinue streaming...");
                    }
                    else if (key.Key == ConsoleKey.D)
                    {
                        Logger.DebugEnabled = !Logger.DebugEnabled;
                    }
                    else
                        ShowControls();
                }
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
                int outputDevice = AudioDeviceSelector.SelectPlaybackDeviceWaveOut();

                using var client = new AudioStreamingClient();

                void ShowControls()
                {
                    Console.WriteLine("\nClient controls:");
                    Console.WriteLine("Q - Stop playback and disconnect");
                    Console.WriteLine("A - Adjust audio during playback");
                    Console.WriteLine("N - Adjust network during playback");
                    Console.WriteLine("R - Recalibrate audio normalization");
                    Console.WriteLine("D - Enable/disable debug logs\n");
                }

                // Добавляем обработчики событий
                client.OnConnected += ShowControls;

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
                    else if (key.Key == ConsoleKey.A)
                    {
                        AudioMainSettings();
                        Console.WriteLine("\nContinue listening...");
                    }
                    else if (key.Key == ConsoleKey.N)
                    {
                        NetworkMainSettings();
                        Console.WriteLine("\nContinue listening...");
                    }
                    else if (key.Key == ConsoleKey.D)
                    {
                        Logger.DebugEnabled = !Logger.DebugEnabled;
                    }
                    else if (key.Key == ConsoleKey.R)
                    {
                        if (AudioSettings._settings.EnableVolumeNormalization)
                            AudioUtils.ResetNormalization();
                        else
                            Console.WriteLine("\nAudio normalization is disabled, please turn it on from audio settings (A) before recalibration");

                        Console.WriteLine("\nContinue listening...");
                    }
                    else
                        ShowControls();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Client error: {ex.Message}", Logger.LogLevel.Error);
            }
        }
    }
}
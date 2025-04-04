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

                string testFile = "D:\\media\\music\\halloween\\halloween.mp3";

                using var reader = new AudioFileReader(testFile);
                using var waveOut = new WaveOutEvent
                {
                    DeviceNumber = deviceIndex // Используем индекс напрямую из WaveOut
                };

                waveOut.Init(reader);
                waveOut.Play();

                var caps = new AudioDeviceSelector.WAVEOUTCAPS();
                AudioDeviceSelector.waveOutGetDevCaps(deviceIndex, ref caps, Marshal.SizeOf(caps));
                Console.WriteLine($"Testing: {caps.szPname} (Index: {deviceIndex})");

                while (waveOut.PlaybackState == PlaybackState.Playing)
                    await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Logger.Log($"Test error: {ex.Message}", Logger.LogLevel.Error);
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
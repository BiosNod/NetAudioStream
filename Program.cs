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

                switch (Console.ReadLine())
                {
                    case "1":
                        await ServerMode();
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
                }
            }
        }

        static async Task TestPlaybackDevice()
        {
            try
            {
                int deviceIndex = AudioDeviceSelector.SelectPlaybackDeviceWave();

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

        static async Task ServerMode()
        {
            try
            {
                var device = AudioDeviceSelector.SelectPlaybackDevice();
                var (ip, port) = NetworkSettings.GetServerSettings();

                using var server = new AudioStreamingServer(ip, port, device);
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
                var outputDevice = AudioDeviceSelector.SelectPlaybackDeviceWave();

                using var client = new AudioStreamingClient();
                await client.ConnectAsync(ip, port, outputDevice); // Теперь передается MMDevice

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
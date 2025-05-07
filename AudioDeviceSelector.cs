using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StreamingApplication
{
    public static class AudioDeviceSelector
    {
        [DllImport("winmm.dll")]
        public static extern int waveOutGetNumDevs();

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        public static extern int waveOutGetDevCaps(int deviceId, ref WAVEOUTCAPS caps, int size);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WAVEOUTCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved;
            public uint dwSupport;
        }

        public static int SelectPlaybackDeviceWaveOut()
        {
            int deviceCount = waveOutGetNumDevs();
            Console.WriteLine("Available Playback Devices (WaveOut):");
            for (int i = 0; i < deviceCount; i++)
            {
                var caps = new WAVEOUTCAPS();
                waveOutGetDevCaps(i, ref caps, Marshal.SizeOf(caps));
                Console.WriteLine($"{i}. {caps.szPname}");
            }

            Console.Write("Select device index: ");
            int deviceIndex = int.Parse(Console.ReadLine()!);
            return deviceIndex;
        }

        public static MMDevice SelectPlaybackDeviceMMD()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

            Console.WriteLine("Available Playback Devices (MMDevice):");
            for (int i = 0; i < devices.Count; i++)
                Console.WriteLine($"{i}. {devices[i].FriendlyName} {devices[i].ID}");

            var device = devices[int.Parse(Console.ReadLine()!)];
            Console.WriteLine($"Selected device: {device}");
            Console.WriteLine($"MixFormat: {device.AudioClient.MixFormat}");
            return device;
        }

        public static void ListPlaybackDevicesWave()
        {
            int deviceCount = waveOutGetNumDevs();
            Console.WriteLine("Available Playback Devices (WaveOut):");
            for (int i = 0; i < deviceCount; i++)
            {
                var caps = new WAVEOUTCAPS();
                waveOutGetDevCaps(i, ref caps, Marshal.SizeOf(caps));
                Console.WriteLine($"{i}. {caps.szPname}");
            }
        }

        public static void ListMMDevices(DataFlow flow)
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active).ToList();
            Console.WriteLine($"Available {flow} Devices (MMDevice):");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i}. {devices[i].FriendlyName} (ID: {devices[i].ID})");
            }
        }

        public static MMDevice SelectRecordingDeviceMMD()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

            Console.WriteLine("Available Recording Devices:");
            for (int i = 0; i < devices.Count; i++)
                Console.WriteLine($"{i}. {devices[i].FriendlyName}");

            var device = devices[int.Parse(Console.ReadLine()!)];
            Console.WriteLine($"Selected device: {device}");
            Console.WriteLine($"MixFormat: {device.AudioClient.MixFormat}");
            return device;
        }

        public static (uint Pid, string Name) SelectProcess()
        {
            using var deviceEnumerator = new MMDeviceEnumerator();
            var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            var processes = new List<(string Name, uint Pid, long Memory)>();

            var sessionManager = device.AudioSessionManager;

            for (int i = 0; i < sessionManager.Sessions.Count; i++)
            {
                var session = sessionManager.Sessions[i];

                if (session.GetProcessID == 0) continue;

                try
                {
                    var process = Process.GetProcessById((int)session.GetProcessID);
                    processes.Add((Name: process.ProcessName, Pid: session.GetProcessID, Memory: process.WorkingSet64)); // Явное именование элементов кортежа
                }
                catch {
                    Logger.Log($"Skip unavailable process PID: {session.GetProcessID}", Logger.LogLevel.Warning);
                }
            }

            var sorted = processes
                //.Where(p => p.Memory >= 1 * 1024 * 1024)
                .OrderByDescending(p => p.Memory)
                .ToList();

            Console.WriteLine("Available Processes:");
            for (int i = 0; i < sorted.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {sorted[i].Name} (PID: {sorted[i].Pid}) - {sorted[i].Memory / 1024 / 1024} MB");
            }

            var selectedIndex = int.Parse(Console.ReadLine()!) - 1;
            Console.WriteLine($"Selected process name: {sorted[selectedIndex].Name}, PID: {sorted[selectedIndex].Pid}");
            return (sorted[selectedIndex].Pid, sorted[selectedIndex].Name);
        }
    }
}
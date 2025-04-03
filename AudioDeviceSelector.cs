using NAudio.CoreAudioApi;
using System.Linq;
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

        public static int SelectPlaybackDeviceWave()
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

        public static MMDevice SelectPlaybackDevice()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

            Console.WriteLine("Available Playback Devices (MMDevice):");
            for (int i = 0; i < devices.Count; i++)
                Console.WriteLine($"{i}. {devices[i].FriendlyName} {devices[i].ID}");

            var result = devices[int.Parse(Console.ReadLine()!)];
            Console.WriteLine($"Selected device: {result}");
            return result;
        }
    }
}
// AudioOutputSelector.cs
using NAudio.CoreAudioApi;

namespace StreamingApplication
{
    public static class AudioOutputSelector
    {
        public static MMDevice SelectPlaybackDevice()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

            Console.WriteLine("Available Playback Devices:");
            for (int i = 0; i < devices.Count; i++)
                Console.WriteLine($"{i + 1}. {devices[i].FriendlyName}");

            return devices[int.Parse(Console.ReadLine()!) - 1];
        }
    }
}
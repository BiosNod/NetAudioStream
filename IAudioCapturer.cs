// IAudioCapturer.cs
using System;

namespace StreamingApplication
{
    public interface IAudioCapturer : IDisposable
    {
        event Action<byte[]> DataAvailable;
        void Start();
        void Stop();
    }
}
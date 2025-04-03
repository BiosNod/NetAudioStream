using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;

namespace StreamingApplication
{
    public class WasapiLoopbackCapturer : IAudioCapturer, IDisposable
    {
        private const double SilenceThreshold = 0.001; // 0.1% от максимальной амплитуды
        private readonly WasapiLoopbackCapture _capture;
        private bool _disposed;

        public event Action<byte[]>? DataAvailable;

        public WasapiLoopbackCapturer(MMDevice device)
        {
            _capture = new WasapiLoopbackCapture(device)
            {
                ShareMode = AudioClientShareMode.Shared,
                WaveFormat = device.AudioClient.MixFormat
            };
            _capture.DataAvailable += OnDataAvailable;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (IsSilence(e.Buffer, e.BytesRecorded))
            {
                Logger.Log("[WASAPI] Silence detected, skipping", Logger.LogLevel.Debug);
                return;
            }

            var buffer = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
            DataAvailable?.Invoke(buffer);
        }

        private bool IsSilence(byte[] buffer, int bytesRecorded)
        {
            if (bytesRecorded == 0) return true;

            double sum = 0;
            int sampleCount = bytesRecorded / 2;

            for (int i = 0; i < bytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                sum += sample * sample;
            }

            double rms = Math.Sqrt(sum / sampleCount) / short.MaxValue;
            return rms < SilenceThreshold;
        }

        public void Start() => _capture.StartRecording();
        public void Stop() => _capture.StopRecording();

        public void Dispose()
        {
            if (_disposed) return;

            _capture.DataAvailable -= OnDataAvailable;
            _capture.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~WasapiLoopbackCapturer() => Dispose();
    }
}
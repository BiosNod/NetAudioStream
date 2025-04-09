using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace StreamingApplication
{
    public class WasapiLoopbackCapturer : IAudioCapturer, IDisposable
    {
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
            Logger.Log($"[WASAPI] Capture format: {_capture.WaveFormat}", Logger.LogLevel.Info);
            _capture.DataAvailable += OnDataAvailable;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_capture.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                Logger.Log($"[WASAPI] Unsupported format: {_capture.WaveFormat}", Logger.LogLevel.Warning);
            }

            if (AudioUtils.IsSilence(e.Buffer, e.BytesRecorded, _capture.WaveFormat))
            {
                Logger.Log("[WASAPI] Silence detected, skipping", Logger.LogLevel.Debug);
                return;
            }

            var buffer = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
            DataAvailable?.Invoke(buffer);
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
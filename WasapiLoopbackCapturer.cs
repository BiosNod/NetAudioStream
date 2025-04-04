using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace StreamingApplication
{
    public class WasapiLoopbackCapturer : IAudioCapturer, IDisposable
    {
        public const double SilenceThreshold = 0.0001; // 0.01% от максимальной амплитуды
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

            // Получаем формат аудио
            var format = _capture.WaveFormat;
            bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;
            int bytesPerSample = format.BitsPerSample / 8;

            double sum = 0;
            int sampleCount = bytesRecorded / bytesPerSample;

            for (int i = 0; i < bytesRecorded; i += bytesPerSample)
            {
                double sampleValue;
                if (isFloat && bytesPerSample == 4)
                {
                    // Для 32-битного float
                    sampleValue = BitConverter.ToSingle(buffer, i);
                }
                else
                {
                    // Для 16-битного PCM (предполагается по умолчанию)
                    short sample = BitConverter.ToInt16(buffer, i);
                    sampleValue = sample / (double)short.MaxValue;
                }

                sum += sampleValue * sampleValue;
            }

            double rms = Math.Sqrt(sum / sampleCount);
            Logger.Log($"[WASAPI] RMS: {rms}", Logger.LogLevel.Debug);
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
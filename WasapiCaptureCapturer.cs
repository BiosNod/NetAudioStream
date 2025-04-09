using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace StreamingApplication
{
    public class WasapiCaptureCapturer : IAudioCapturer
    {
        private readonly WasapiCapture _capture;
        public event Action<byte[]>? DataAvailable;

        public WasapiCaptureCapturer(MMDevice captureDevice)
        {
            _capture = new WasapiCapture(captureDevice);
            _capture.DataAvailable += (s, e) =>
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

                var buffer = e.Buffer.Take(e.BytesRecorded).ToArray();
                DataAvailable?.Invoke(buffer);
            };
        }

        public void Start() => _capture.StartRecording();
        public void Stop() => _capture.StopRecording();

        public void Dispose()
        {
            _capture.Dispose();
            GC.SuppressFinalize(this);
        }
    }

}

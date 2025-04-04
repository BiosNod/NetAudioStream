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
                DataAvailable?.Invoke(e.Buffer.Take(e.BytesRecorded).ToArray());
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

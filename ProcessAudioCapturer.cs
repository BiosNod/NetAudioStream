// ProcessAudioCapturer.cs
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static System.Collections.Specialized.BitVector32;

namespace StreamingApplication
{
    public class ProcessAudioCapturer : IAudioCapturer
    {
        private readonly MMDevice _device;
        private readonly uint _targetProcessId;
        private WasapiLoopbackCapture _capture;
        private List<AudioSessionControl> _sessions = new List<AudioSessionControl>();

        public event Action<byte[]> DataAvailable = delegate { };

        public ProcessAudioCapturer(MMDevice device, uint processId)
        {
            _device = device;
            _targetProcessId = processId;

            _capture = new WasapiLoopbackCapture(device)
            {
                ShareMode = AudioClientShareMode.Shared,
                WaveFormat = device.AudioClient.MixFormat
            };

            InitializeSessions();
            _capture.DataAvailable += Capture_DataAvailable;
        }

        private void InitializeSessions()
        {
            var sessionManager = _device.AudioSessionManager;
            for (int i = 0; i < sessionManager.Sessions.Count; i++)
            {
                var session = sessionManager.Sessions[i];
                if (session.GetProcessID == _targetProcessId)
                {
                    _sessions.Add(session);
                }
            }
        }

        private void Capture_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_sessions.Any(s => s.State == AudioSessionState.AudioSessionStateActive))
            {
                DataAvailable?.Invoke(e.Buffer.Take(e.BytesRecorded).ToArray());
            }
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
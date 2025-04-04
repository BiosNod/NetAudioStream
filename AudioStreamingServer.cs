using System.Net;
using System.Net.Sockets;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace StreamingApplication
{
    public class AudioStreamingServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly List<NetworkStream> _clientStreams = new List<NetworkStream>();
        private readonly IAudioCapturer _capturer;
        private readonly object _syncLock = new object();
        private bool _isRunning;
        private WaveFormat _serverWaveFormat;

        public AudioStreamingServer(string ip, int port, MMDevice inputDevice, DataFlow flow, uint processId = 0)
        {
            _listener = new TcpListener(IPAddress.Parse(ip), port);

            if (flow == DataFlow.Render)
            {
                if (processId > 0)
                    _capturer = new ProcessAudioCapturer(inputDevice, processId);
                else
                    _capturer = new WasapiLoopbackCapturer(inputDevice);
            }
            else
            {
                _capturer = new WasapiCaptureCapturer(inputDevice);
            }

            _serverWaveFormat = inputDevice.AudioClient.MixFormat;
            _capturer.DataAvailable += OnAudioDataAvailable;
        }

        public void Start()
        {
            _isRunning = true;
            _capturer.Start();
            _listener.Start();
            Task.Run(AcceptClientsLoop);
        }

        private async Task AcceptClientsLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    var stream = client.GetStream();

                    // Отправляем параметры формата новому клиенту
                    SendWaveFormat(stream, _serverWaveFormat);

                    lock (_syncLock)
                    {
                        _clientStreams.Add(stream);
                    }
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    Logger.Log($"Accept error: {ex.Message}", Logger.LogLevel.Error);
                }
            }
        }

        private void SendWaveFormat(NetworkStream stream, WaveFormat waveFormat)
        {
            byte[] formatData = new byte[12];
            BitConverter.GetBytes(waveFormat.SampleRate).CopyTo(formatData, 0);
            BitConverter.GetBytes(waveFormat.BitsPerSample).CopyTo(formatData, 4);
            BitConverter.GetBytes(waveFormat.Channels).CopyTo(formatData, 8);

            stream.Write(formatData, 0, formatData.Length);
        }

        private void OnAudioDataAvailable(byte[] data)
        {
            try
            {
                var encryptedData = EncryptionHelper.Encrypt(data);
                SendToAllClients(encryptedData);
            }
            catch (Exception ex)
            {
                Logger.Log($"Audio error: {ex.Message}", Logger.LogLevel.Error);
            }
        }

        private void SendToAllClients(byte[] data)
        {
            Logger.Log($"Send bytes length: {data.Length}", Logger.LogLevel.Debug);
            List<NetworkStream> deadClients = new List<NetworkStream>();

            lock (_syncLock)
            {
                foreach (var stream in _clientStreams)
                {
                    try
                    {
                        var lengthHeader = BitConverter.GetBytes(data.Length);
                        stream.Write(lengthHeader, 0, 4);
                        stream.Write(data, 0, data.Length);
                    }
                    catch
                    {
                        deadClients.Add(stream);
                    }
                }

                foreach (var dead in deadClients)
                {
                    _clientStreams.Remove(dead);
                    dead.Dispose();
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _capturer.Stop();
            _listener.Stop();

            lock (_syncLock)
            {
                foreach (var stream in _clientStreams)
                {
                    stream.Dispose();
                }
                _clientStreams.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
            _capturer.Dispose();
            GC.SuppressFinalize(this);
        }

        ~AudioStreamingServer() => Dispose();
    }
}
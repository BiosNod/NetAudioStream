using NAudio.Wave;
using NAudio.Lame;
using System.Net;
using System.Net.Sockets;
using NAudio.CoreAudioApi;
using NAudio.Gui;
using NAudio.Wave;

namespace StreamingApplication
{
    public class AudioStreamingServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly int _port;
        private readonly List<NetworkStream> _clientStreams = new();
        private readonly IAudioCapturer _capturer;
        private readonly object _syncLock = new();
        private bool _isRunning;
        private WaveFormat _serverWaveFormat;

        private MemoryStream _audioBuffer = new MemoryStream();
        private readonly object _bufferLock = new object();
        private Timer _sendTimer;
        private bool _processing = false;

        public AudioStreamingServer(string ip, int port, MMDevice inputDevice, DataFlow flow, uint processId = 0)
        {
            _port = port;
            _listener = new TcpListener(IPAddress.Parse(ip), port);

            if (flow == DataFlow.Render)
            {
                _capturer = processId > 0
                    ? new ProcessAudioCapturer(inputDevice, processId)
                    : new WasapiLoopbackCapturer(inputDevice);
            }
            else
            {
                _capturer = new WasapiCaptureCapturer(inputDevice);
            }

            _serverWaveFormat = inputDevice.AudioClient.MixFormat;
            _capturer.DataAvailable += OnAudioDataAvailable;
            var interval = AudioSettings._settings.ServerLatency;
            _sendTimer = new Timer(SendBufferedData, null, interval, interval);
        }

        public async void Start()
        {
            _isRunning = true;

            // Проброска порта через UPnP
            if (_port > 0)
            {
                await NetworkSettings.TryForwardPort(_port, "NetAudioStream Server");
            }

            _capturer.Start();
            _listener.Start();
            Logger.Log($"Server started on {_listener.LocalEndpoint}", Logger.LogLevel.Info);
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
                        Logger.Log($"Client connected from {client.Client.RemoteEndPoint?.ToString()} (Total clients: {_clientStreams.Count})", Logger.LogLevel.Info);
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
            lock (_bufferLock)
            {
                _audioBuffer.Write(data, 0, data.Length);
            }
        }

        private void SendBufferedData(object state)
        {
            if (_processing) return;
            _processing = true;

            try
            {
                byte[] bufferedData;
                lock (_bufferLock)
                {
                    if (_audioBuffer.Length == 0) return;

                    bufferedData = _audioBuffer.ToArray();
                    _audioBuffer.SetLength(0); // Очищаем буфер
                }

                ProcessAndSendData(bufferedData);
            }
            finally
            {
                _processing = false;
            }
        }

        // Вынесенная логика обработки данных
        private void ProcessAndSendData(byte[] data)
        {
            try
            {
                byte[] processedData = data;

                if (AudioSettings._settings.EnableCompression)
                {
                    processedData = CompressAudio(data);
                    if (processedData == null || processedData.Length == 0)
                    {
                        Logger.Log("Skipping invalid compressed data", Logger.LogLevel.Warning);
                        return;
                    }
                }

                var encryptedData = EncryptionHelper.Encrypt(processedData);
                SendToAllClients(encryptedData);
            }
            catch (Exception ex)
            {
                Logger.Log($"Audio processing error: {ex.Message}", Logger.LogLevel.Error);
            }
        }

        private byte[]? CompressAudio(byte[] pcmData)
		{
			try
			{
				var sourceFormat = _serverWaveFormat;
				var targetFormat = new WaveFormat(44100, 16, 2);

				Logger.Log($"Converting audio from {sourceFormat} to {targetFormat}", Logger.LogLevel.Debug);

				using var inputStream = new MemoryStream(pcmData);
				using var reader = new RawSourceWaveStream(inputStream, sourceFormat);
				using var resampler = new MediaFoundationResampler(reader, targetFormat);
				resampler.ResamplerQuality = 60;

				using var outputStream = new MemoryStream();
				using var writer = new LameMP3FileWriter(outputStream, targetFormat, AudioSettings._settings.Bitrate);

				byte[] buffer = new byte[4096];
				int bytesRead;
				while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
				{
					writer.Write(buffer, 0, bytesRead);
				}

				writer.Close();
				return outputStream.ToArray();
			}
			catch (Exception ex)
			{
				Logger.Log($"Compression failed: {ex.Message}", Logger.LogLevel.Error);
				return null;
			}
		}

        private void SendToAllClients(byte[] data)
        {
            Logger.Log($"Send bytes length: {data.Length}", Logger.LogLevel.Debug);
            List<NetworkStream> deadClients = new();

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
                    Logger.Log($"Client disconnected (Remaining: {_clientStreams.Count})", Logger.LogLevel.Info);
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _capturer.Stop();
            _listener.Stop();
            _sendTimer?.Dispose();

            // Отправляем оставшиеся данные
            SendBufferedData(null);

            // Удаление проброски порта
            if (_port > 0)
            {
                _ = NetworkSettings.RemovePortForward(_port);
            }

            lock (_syncLock)
            {
                foreach (var stream in _clientStreams)
                {
                    stream.Dispose();
                }
                _clientStreams.Clear();
            }
        }

        public void Dispose() => Stop();
    }
}
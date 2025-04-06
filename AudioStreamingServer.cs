using NAudio.Wave;
using System.Net;
using System.Net.Sockets;
using NAudio.CoreAudioApi;
using Concentus.Structs;
using Concentus.Enums;


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
                var targetFormat = new WaveFormat(48000, 16, 2); // Opus лучше работает с частотой 48kHz

                Logger.Log($"Converting audio from {sourceFormat} to {targetFormat} using Opus", Logger.LogLevel.Debug);

                using var inputStream = new MemoryStream(pcmData);
                using var reader = new RawSourceWaveStream(inputStream, sourceFormat);
                using var resampler = new MediaFoundationResampler(reader, targetFormat);
                resampler.ResamplerQuality = 60;

                using var outputStream = new MemoryStream();

                // Создаем кодировщик Opus
                var encoder = new OpusEncoder(targetFormat.SampleRate, targetFormat.Channels, OpusApplication.OPUS_APPLICATION_AUDIO);

                // Настройка битрейта (в бит/сек)
                encoder.Bitrate = AudioSettings._settings.Bitrate * 1000; // Переводим из кбит/с в бит/с

                // Размер фрейма (20 мс аудио при 48кГц)
                int frameSize = targetFormat.SampleRate / 50; // 20мс фрейм

                // Буферы для PCM данных и кодированных данных
                short[] pcmBuffer = new short[frameSize * targetFormat.Channels]; // Стерео
                byte[] byteBuffer = new byte[pcmBuffer.Length * 2];  // 16 бит на сэмпл = 2 байта
                byte[] opusPacket = new byte[1275]; // Максимальный размер Opus пакета

                int bytesRead;

                // Сначала запишем заголовок с информацией о формате
                BinaryWriter writer = new BinaryWriter(outputStream);
                writer.Write(targetFormat.SampleRate);
                writer.Write(targetFormat.Channels);

                while ((bytesRead = resampler.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
                {
                    // Конвертируем байты в short samples для Opus
                    int shortSamplesCount = Math.Min(pcmBuffer.Length, bytesRead / 2);
                    for (int i = 0; i < shortSamplesCount; i++)
                    {
                        pcmBuffer[i] = BitConverter.ToInt16(byteBuffer, i * 2);
                    }

                    // Кодируем фрейм в Opus
                    int packetLength = encoder.Encode(pcmBuffer, 0, frameSize, opusPacket, 0, opusPacket.Length);

                    if (packetLength > 0)
                    {
                        // Записываем размер пакета и сам пакет
                        writer.Write(packetLength);
                        writer.Write(opusPacket, 0, packetLength);
                    }
                }

                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Log($"Opus compression failed: {ex.Message}", Logger.LogLevel.Error);
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
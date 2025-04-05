using NAudio.Wave;
using System.Net.Sockets;

namespace StreamingApplication
{
    public class AudioStreamingClient : IDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private WaveOutEvent? _outputDevice;
        private BufferedWaveProvider? _waveProvider;
        private bool _isConnected;
        private int _selectedDevice;
        private MemoryStream? _audioBuffer;
        private string _serverIp = string.Empty;
        private int _serverPort;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 5;
        private const int ReconnectDelayMs = 5000;
        private CancellationTokenSource? _cts;

        public event Action? OnConnected;
        public event Action<string>? OnDisconnected;
        public event Action<string>? OnReconnecting;

        #region Public Methods
        public async Task ConnectAsync(string ip, int port, int outputDevice)
        {
            _serverIp = ip;
            _serverPort = port;
            _selectedDevice = outputDevice;
            _cts = new CancellationTokenSource();

            await TryConnectWithRetry();
        }

        private async Task TryConnectWithRetry()
        {
            while (_reconnectAttempts < MaxReconnectAttempts && !(_cts?.IsCancellationRequested ?? true))
            {
                try
                {
                    _audioBuffer = new MemoryStream(2 * 1024 * 1024);
                    await InitializeNetworkConnection(_serverIp, _serverPort);

                    var (sampleRate, bits, channels) = await ReceiveWaveFormat();
                    InitializeAudioDevice(sampleRate, bits, channels);

                    _reconnectAttempts = 0; // Сброс счетчика при успешном подключении
                    OnConnected?.Invoke();
                    StartReceivingData();
                    Logger.Log($"Connected to {_serverIp}:{_serverPort}", Logger.LogLevel.Info);
                    return;
                }
                catch (Exception ex)
                {
                    _reconnectAttempts++;
                    if (_reconnectAttempts >= MaxReconnectAttempts)
                    {
                        Logger.Log($"Max reconnect attempts reached. Giving up.", Logger.LogLevel.Error);
                        OnDisconnected?.Invoke("Max reconnect attempts reached");
                        Disconnect();
                        return;
                    }

                    OnReconnecting?.Invoke($"Attempt {_reconnectAttempts} of {MaxReconnectAttempts}");
                    Logger.Log($"Connection failed ({_reconnectAttempts}/{MaxReconnectAttempts}): {ex.Message}. Retrying in {ReconnectDelayMs / 1000} seconds...",
                             Logger.LogLevel.Warning);

                    await Task.Delay(ReconnectDelayMs, _cts?.Token ?? CancellationToken.None);
                }
            }
        }


        private async Task<(int, int, int)> ReceiveWaveFormat()
        {
            byte[] formatData = new byte[12];
            int bytesRead = 0;

            while (bytesRead < 12)
            {
                int read = await _stream!.ReadAsync(formatData, bytesRead, 12 - bytesRead);
                if (read == 0) throw new Exception("Failed to receive wave format");
                bytesRead += read;
            }

            return (
                BitConverter.ToInt32(formatData, 0),
                BitConverter.ToInt32(formatData, 4),
                BitConverter.ToInt32(formatData, 8)
            );
        }

        private void InitializeAudioDevice(int sampleRate, int bitsPerSample, int channels)
        {
            _outputDevice = new WaveOutEvent
            {
                DeviceNumber = _selectedDevice,
                DesiredLatency = 200
            };

            WaveFormat serverWaveFormat;


            if (AudioSettings.GetCompressionSettings().enabled)
            {

                sampleRate = 44100;
                bitsPerSample = 16;
                channels = 2;
            }

            // Создаем формат с IEEE Float и WAVE_FORMAT_EXTENSIBLE
            Console.WriteLine($"Init audio, rate: {sampleRate}, bits: {bitsPerSample}, channels: {channels}");
            serverWaveFormat = new WaveFormatExtensible(sampleRate, bitsPerSample, channels);

            //var customWaveFormat = AudioDeviceSelector.SelectPlaybackDevice().AudioClient.MixFormat;
            //Console.WriteLine($"{serverWaveFormat} vs {customWaveFormat}");
            _waveProvider = new BufferedWaveProvider(serverWaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(60),
                DiscardOnBufferOverflow = true
            };

            _outputDevice.Init(_waveProvider);
            _outputDevice.Play();
        }

        public void Disconnect()
        {
            if (!_isConnected && _reconnectAttempts == 0) return;

            _cts?.Cancel();
            _isConnected = false;
            Logger.Log("Disconnecting...", Logger.LogLevel.Info);

            _outputDevice?.Stop();
            _client?.Close();

            _stream?.Dispose();
            _outputDevice?.Dispose();
            _audioBuffer?.Dispose();

            OnDisconnected?.Invoke("Disconnected by user");
        }

        public void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Private Methods
        private async Task InitializeNetworkConnection(string ip, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(ip, port);
            _stream = _client.GetStream();
            _isConnected = true;
        }

        private void StartReceivingData()
        {
            Task.Run(async () =>
            {
                try
                {
                    await ReceiveDataLoop();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Receive loop error: {ex.Message}", Logger.LogLevel.Error);
                    HandleDisconnection();
                }
            });
        }

        private void HandleDisconnection()
        {
            if (!_isConnected) return;

            _isConnected = false;
            OnDisconnected?.Invoke("Connection lost");

            if (!(_cts?.IsCancellationRequested ?? true))
            {
                Logger.Log("Attempting to reconnect...", Logger.LogLevel.Info);
                _ = TryConnectWithRetry();
            }
        }

        private async Task ReceiveDataLoop()
        {
            var headerBuffer = new byte[4];

            while (_isConnected && !(_cts?.IsCancellationRequested ?? true))
            {
                try
                {
                    await ReadPacketHeader(headerBuffer);
                    int packetSize = BitConverter.ToInt32(headerBuffer, 0);

                    await ProcessAudioPacket(packetSize);
                    AdjustBufferSize();
                }
                catch (Exception ex) when (ex is SocketException or IOException)
                {
                    Logger.Log($"Network error: {ex.Message}", Logger.LogLevel.Warning);
                    HandleDisconnection();
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Processing error: {ex.Message}", Logger.LogLevel.Error);
                }
            }
        }

        private async Task ReadPacketHeader(byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < 4 && _isConnected)
            {
                int bytesRead = await _stream!.ReadAsync(buffer, totalRead, 4 - totalRead);
                if (bytesRead == 0) throw new SocketException();
                totalRead += bytesRead;
            }
        }

        private async Task ProcessAudioPacket(int packetSize)
        {
            Logger.Log($"Process packet with bytes length: {packetSize}", Logger.LogLevel.Debug);
            var encryptedData = new byte[packetSize];
            int totalRead = 0;

            while (totalRead < packetSize && _isConnected)
            {
                int bytesRead = await _stream!.ReadAsync(
                    encryptedData,
                    totalRead,
                    packetSize - totalRead
                );

                if (bytesRead == 0) throw new SocketException();
                totalRead += bytesRead;
            }

            var decryptedData = EncryptionHelper.Decrypt(encryptedData);
            Logger.Log($"Packet decrypted", Logger.LogLevel.Debug);

            byte[] audioData = AudioSettings.GetCompressionSettings().enabled
                ? DecompressAudio(decryptedData)
                : decryptedData;

            _waveProvider!.AddSamples(audioData, 0, audioData.Length);
        }

        private byte[] DecompressAudio(byte[] mp3Data)
        {
            try
            {
                using var mp3Stream = new MemoryStream(mp3Data);
                using var reader = new Mp3FileReader(mp3Stream);
                using var waveStream = WaveFormatConversionStream.CreatePcmStream(reader);
                using var outputStream = new MemoryStream();
                
                var buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = waveStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outputStream.Write(buffer, 0, bytesRead);
                }
                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Log($"Decompression failed: {ex.Message}", Logger.LogLevel.Error);
                return Array.Empty<byte>();
            }
        }

        private void AdjustBufferSize()
        {
            if (_waveProvider!.BufferedDuration.TotalSeconds > 25)
            {
                _waveProvider.ClearBuffer();
                Logger.Log("Buffer cleared", Logger.LogLevel.Debug);
            }
        }
        #endregion
    }
}
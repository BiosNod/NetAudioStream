using NAudio.Wave;
using System.Net.Sockets;

namespace StreamingApplication
{
    public class AudioStreamingClient : IDisposable
    {
        #region Fields
        private TcpClient? _client;
        private NetworkStream? _stream;
        private WaveOutEvent? _outputDevice;
        private BufferedWaveProvider? _waveProvider;
        private bool _isConnected;
        private int _selectedDevice;
        private MemoryStream _audioBuffer;
        #endregion

        #region Public Methods
        public async Task ConnectAsync(string ip, int port, int outputDevice)
        {
            try
            {
                _selectedDevice = outputDevice;
                _audioBuffer = new MemoryStream(2 * 1024 * 1024);

                await InitializeNetworkConnection(ip, port);

                // Получаем параметры формата
                var (sampleRate, bits, channels) = await ReceiveWaveFormat();
                InitializeAudioDevice(sampleRate, bits, channels);

                StartReceivingData();
                Logger.Log($"Connected to {ip}:{port}", Logger.LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"Connection error: {ex.Message}", Logger.LogLevel.Error);
                Disconnect();
                throw;
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

            Console.WriteLine($"Init audio, rate: {sampleRate}, bits: {bitsPerSample}, channels: {channels}");

            // Создаем формат с IEEE Float и WAVE_FORMAT_EXTENSIBLE
            var serverWaveFormat = new WaveFormatExtensible(
                sampleRate,
                bitsPerSample,
                channels
            );

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
            if (!_isConnected) return;

            _isConnected = false;
            Logger.Log("Disconnecting...", Logger.LogLevel.Info);

            _outputDevice?.Stop();
            _client?.Close();

            _stream?.Dispose();
            _outputDevice?.Dispose();
            _audioBuffer?.Dispose();
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
                    Disconnect();
                }
            });
        }

        private async Task ReceiveDataLoop()
        {
            var headerBuffer = new byte[4];

            while (_isConnected)
            {
                try
                {
                    await ReadPacketHeader(headerBuffer);
                    int packetSize = BitConverter.ToInt32(headerBuffer, 0);

                    await ProcessAudioPacket(packetSize);
                    AdjustBufferSize();
                }
                catch (SocketException)
                {
                    Logger.Log("Connection lost", Logger.LogLevel.Warning);
                    Disconnect();
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
            //Logger.Log($"Process packet with bytes length: {packetSize}", Logger.LogLevel.Debug);
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
            _waveProvider!.AddSamples(decryptedData, 0, decryptedData.Length);
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
using NAudio.Wave;
using System.Net.Sockets;
using Concentus.Structs;
using Concentus.Enums;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.IO.Compression;
using static System.Windows.Forms.DataFormats;

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
        private const int MaxReconnectAttempts = 1000;
        private const int ReconnectDelayMs = 3000;
        private CancellationTokenSource? _userCancelCts;

        private const int ConnectionTimeoutMs = 5000;
        private CancellationTokenSource? _connectCts;

        public event Action? OnConnected;
        public event Action<string>? OnDisconnected;
        public event Action<string>? OnReconnecting;

        public async Task ConnectAsync(string ip, int port, int outputDevice)
        {
            _serverIp = ip;
            _serverPort = port;
            _selectedDevice = outputDevice;

            // Start key monitoring in a separate task
            var keyMonitorCts = new CancellationTokenSource();
            var keyMonitorTask = Task.Run(() => MonitorKeyPresses(keyMonitorCts.Token));

            try
            {
                await TryConnectWithRetry();
            }
            finally
            {
                // Stop key monitoring
                keyMonitorCts.Cancel();
                await keyMonitorTask;
                keyMonitorCts.Dispose();
            }
        }

        private void MonitorKeyPresses(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                {
                    _userCancelCts?.Cancel();
                    Console.WriteLine("\nConnection attempt cancelled by user!");
                    break;
                }
                Thread.Sleep(100);
            }
        }

        private async Task TryConnectWithRetry()
        {
            // Create a new user cancellation token source if needed
            if (_userCancelCts == null || _userCancelCts.IsCancellationRequested)
                _userCancelCts = new CancellationTokenSource();

            while (_reconnectAttempts < MaxReconnectAttempts && !_userCancelCts.IsCancellationRequested)
            {
                try
                {
                    _audioBuffer = new MemoryStream(2 * 1024 * 1024);

                    // Create a new timeout source for this connection attempt
                    using var timeoutCts = new CancellationTokenSource(ConnectionTimeoutMs);
                    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        _userCancelCts.Token, timeoutCts.Token);

                    try
                    {
                        // Connection attempt with combined token
                        await InitializeNetworkConnection(_serverIp, _serverPort, combinedCts.Token);

                        // Get audio format parameters
                        var (sampleRate, bits, channels) = await ReceiveWaveFormat();

                        // Initialize audio device
                        InitializeAudioDevice(sampleRate, bits, channels);

                        _reconnectAttempts = 0; // Reset counter on successful connection
                        OnConnected?.Invoke();
                        StartReceivingData();
                        Logger.Log($"Connected to {_serverIp}:{_serverPort}", Logger.LogLevel.Info);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        // Check which token triggered the cancellation
                        if (_userCancelCts.IsCancellationRequested)
                        {
                            Logger.Log("Connection cancelled by user", Logger.LogLevel.Warning);
                            throw; // Rethrow to exit the method
                        }
                        else if (timeoutCts.IsCancellationRequested)
                        {
                            Logger.Log($"Connection to {_serverIp}:{_serverPort} timed out ({ConnectionTimeoutMs}ms)",
                                Logger.LogLevel.Warning);
                            // Continue to retry
                        }
                    }
                }
                catch (Exception ex) when (ex is SocketException or IOException)
                {
                    Logger.Log($"Network error: {ex.Message}", Logger.LogLevel.Warning);
                }
                catch (OperationCanceledException) when (_userCancelCts.IsCancellationRequested)
                {
                    // User cancelled, exit the retry loop
                    Logger.Log("Connection attempts cancelled by user", Logger.LogLevel.Info);
                    OnDisconnected?.Invoke("Cancelled by user");
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Connection error: {ex.Message}", Logger.LogLevel.Error);
                }

                _reconnectAttempts++;
                if (_reconnectAttempts >= MaxReconnectAttempts)
                {
                    Logger.Log($"Max reconnect attempts reached. Giving up.", Logger.LogLevel.Error);
                    OnDisconnected?.Invoke("Max reconnect attempts reached");
                    Disconnect();
                    return;
                }

                OnReconnecting?.Invoke($"Attempt {_reconnectAttempts} of {MaxReconnectAttempts}");
                Logger.Log($"Connection failed ({_reconnectAttempts}/{MaxReconnectAttempts}). Retrying in {ReconnectDelayMs / 1000} seconds...",
                         Logger.LogLevel.Warning);

                await Task.Delay(ReconnectDelayMs, _userCancelCts.Token);
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
                DesiredLatency = AudioSettings._settings.ClientLatency
            };

            WaveFormat serverWaveFormat;

            // Создаем формат с IEEE Float и WAVE_FORMAT_EXTENSIBLE
            Console.WriteLine($"Init audio, rate: {sampleRate}, bits: {bitsPerSample}, channels: {channels}");
            serverWaveFormat = new WaveFormatExtensible(sampleRate, bitsPerSample, channels);

            //var customWaveFormat = AudioDeviceSelector.SelectPlaybackDevice().AudioClient.MixFormat;
            //Console.WriteLine($"{serverWaveFormat} vs {customWaveFormat}");
            _waveProvider = new BufferedWaveProvider(serverWaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };

            _outputDevice.Init(_waveProvider);
            _outputDevice.Play();
        }

        public void Disconnect()
        {
            if (!_isConnected && _reconnectAttempts == 0) return;

            _userCancelCts?.Cancel();
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

        private async Task InitializeNetworkConnection(string ip, int port, CancellationToken cancellationToken)
        {
            _client = new TcpClient();
            try
            {
                await _client.ConnectAsync(ip, port, cancellationToken);
                _stream = _client.GetStream();
                _isConnected = true;
            }
            catch
            {
                _client.Dispose();
                _client = null;
                throw;
            }
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
            Logger.Log($"Disconnected from server {_serverIp}:{_serverPort}", Logger.LogLevel.Info);

            if (!(_userCancelCts?.IsCancellationRequested ?? true))
            {
                Logger.Log("Attempting to reconnect...", Logger.LogLevel.Info);
                _ = TryConnectWithRetry();
            }
        }

        private async Task ReceiveDataLoop()
        {
            var headerBuffer = new byte[4];

            while (_isConnected && !(_userCancelCts?.IsCancellationRequested ?? true))
            {
                try
                {
                    await ReadPacketHeader(headerBuffer);
                    int packetSize = BitConverter.ToInt32(headerBuffer, 0);
                    await ProcessAudioPacket(packetSize);
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

            byte[] audioData = AudioSettings._settings.EnableCompression
                ? DecompressAudio(decryptedData)
                : decryptedData;

            // Если нормализация включена
            if (AudioSettings._settings.EnableVolumeNormalization)
            {
                audioData = AudioUtils.NormalizeVolume(audioData, _waveProvider?.WaveFormat);
            }

            // Если нормализация отключена, но включен контроль громкости, то просто применяем регулировку громкости
            if (AudioSettings._settings.EnableVolumeControl)
            {
                audioData = AudioUtils.ApplyGain(audioData, AudioSettings._settings.VolumeLevel, _waveProvider?.WaveFormat);
            }

            _waveProvider!.AddSamples(audioData, 0, audioData.Length);
        }

        public byte[] DecompressAudio(byte[] compressedData)
        {
            using var inputStream = new MemoryStream(compressedData);
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(outputStream);
            }
            return outputStream.ToArray();
        }
    }
}
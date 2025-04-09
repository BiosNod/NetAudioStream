using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;

namespace StreamingApplication
{
    public class ProcessAudioCapturer : IAudioCapturer, IDisposable
    {
        private readonly uint _targetProcessId;
        private Process _loopbackProcess;
        private CancellationTokenSource _cts;
        private Task _readTask;
        private bool _isRunning = false;
        private bool _disposed = false;
        public static string ApplicationLoopbackPath = "ApplicationLoopback.exe";

        public event Action<byte[]> DataAvailable = delegate { };

        /// <summary>
        /// Creates a new ProcessAudioCapturer that uses ApplicationLoopback.exe
        /// </summary>
        /// <param name="device">Audio device (not used in this implementation but kept for interface compatibility)</param>
        /// <param name="processId">Target process ID to capture audio from</param>
        public ProcessAudioCapturer(uint processId)
        {
            _targetProcessId = processId;
        }

        /// <summary>
        /// Starts capturing audio from the target process
        /// </summary>
        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ApplicationLoopbackPath)))
            {
                try { process.Kill(); } catch { }
            }

            // Configure the process to run ApplicationLoopback.exe
            _loopbackProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ApplicationLoopbackPath,
                    Arguments = $"{_targetProcessId} includetree -stream -silence -skipheaders",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                },
                EnableRaisingEvents = true
            };

            // Start the process
            _loopbackProcess.Start();

            // Start reading the output in a separate task
            _readTask = Task.Run(() => ReadOutputStreamAsync(_cts.Token), _cts.Token);
        }

        /// <summary>
        /// Stops capturing audio
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            // Cancel the reading task
            _cts?.Cancel();

            try
            {
                // Try to gracefully stop the process first
                if (!_loopbackProcess.HasExited)
                {
                    // Send 'Q' key to stop the process as per ApplicationLoopback's design
                    _loopbackProcess.StandardInput.Write('Q');

                    // Give it a moment to exit gracefully
                    if (!_loopbackProcess.WaitForExit(500))
                    {
                        _loopbackProcess.Kill();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping loopback process: {ex.Message}");
                // Try to kill the process if graceful shutdown failed
                try { _loopbackProcess?.Kill(); } catch { }
            }

            // Wait for the reading task to complete
            try { _readTask?.Wait(500); } catch { }
        }

        /// <summary>
        /// Continuously reads from the process output stream and raises DataAvailable events
        /// </summary>
        private async Task ReadOutputStreamAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Buffer for reading output
                byte[] buffer = new byte[16384]; // 16KB buffer
                Stream outputStream = _loopbackProcess.StandardOutput.BaseStream;

                while (!cancellationToken.IsCancellationRequested && !_loopbackProcess.HasExited)
                {
                    int bytesRead = await outputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead > 0)
                    {
                        // Create a copy of the buffer to avoid issues with buffer reuse
                        byte[] audioData = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, audioData, 0, bytesRead);

                        // Raise the event with the captured audio data
                        DataAvailable?.Invoke(audioData);
                    }
                    else if (bytesRead == 0)
                    {
                        // End of stream or process exited
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading from ApplicationLoopback: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();

            _cts?.Dispose();
            _loopbackProcess?.Dispose();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
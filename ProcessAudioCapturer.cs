using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace StreamingApplication
{
    public class ProcessLoopbackCapturer : IAudioCapturer, IDisposable
    {
        private readonly uint _processId;
        private MMDevice _device;
        private WasapiCapture _capture;
        private bool _isCapturing;

        public event Action<byte[]> DataAvailable = delegate { };

        public ProcessLoopbackCapturer(uint processId, bool includeProcessTree = true)
        {
            _processId = processId;
            Logger.Log($"Creating capturer for PID: {processId}", Logger.LogLevel.Debug);
        }

        public void Start()
        {
            Logger.Log($"Starting audio capture for PID: {_processId}", Logger.LogLevel.Debug);

            try
            {
                // Check if process exists
                try
                {
                    Process.GetProcessById((int)_processId);
                }
                catch
                {
                    Logger.Log($"Process with ID {_processId} does not exist", Logger.LogLevel.Error);
                    return;
                }

                // Create device enumerator
                var deviceEnumerator = new MMDeviceEnumerator();
                _device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                Logger.Log($"Using audio device: {_device.FriendlyName}", Logger.LogLevel.Debug);

                // Create WASAPI capture
                _capture = new WasapiLoopbackCapture(_device);

                // Subscribe to data available event
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;

                // Start capturing
                _isCapturing = true;
                _capture.StartRecording();

                Logger.Log("Audio capture started successfully", Logger.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error starting audio capture: {ex}", Logger.LogLevel.Error);
                CleanUp();
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                if (_isCapturing && e.BytesRecorded > 0)
                {
                    // Create a copy of the data to ensure it's not modified
                    var buffer = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, buffer, e.BytesRecorded);

                    // Raise event
                    DataAvailable?.Invoke(buffer);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in OnDataAvailable: {ex}", Logger.LogLevel.Error);
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            Logger.Log("Recording stopped", Logger.LogLevel.Debug);

            if (e.Exception != null)
            {
                Logger.Log($"Recording stopped with exception: {e.Exception}", Logger.LogLevel.Error);
            }

            _isCapturing = false;
        }

        public void Stop()
        {
            Logger.Log("Stopping audio capture", Logger.LogLevel.Debug);
            _isCapturing = false;

            try
            {
                _capture?.StopRecording();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error stopping recording: {ex}", Logger.LogLevel.Error);
            }

            CleanUp();
        }

        private void CleanUp()
        {
            Logger.Log("Cleaning up resources", Logger.LogLevel.Debug);

            try
            {
                if (_capture != null)
                {
                    _capture.DataAvailable -= OnDataAvailable;
                    _capture.RecordingStopped -= OnRecordingStopped;
                    _capture.Dispose();
                    _capture = null;
                }

                if (_device != null)
                {
                    _device.Dispose();
                    _device = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error cleaning up resources: {ex}", Logger.LogLevel.Error);
            }
        }

        public void Dispose()
        {
            Logger.Log("Disposing ProcessLoopbackCapturer", Logger.LogLevel.Debug);
            Stop();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Alternative implementation that uses audio filtering to focus on audio from a specific process
    /// Note: This doesn't isolate audio perfectly but avoids using complex Windows APIs
    /// </summary>
    public class ProcessAudioCapture : IAudioCapturer, IDisposable
    {
        private readonly string _processName;
        private WasapiLoopbackCapture _capture;
        private bool _isCapturing;
        private System.Timers.Timer _processMonitorTimer;
        private bool _isTargetProcessRunning;

        public event Action<byte[]> DataAvailable = delegate { };

        public ProcessAudioCapture(uint processId)
        {
            try
            {
                var process = Process.GetProcessById((int)processId);
                _processName = process.ProcessName;
                Logger.Log($"Creating audio capturer for process: {_processName} (PID: {processId})", Logger.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting process info: {ex}", Logger.LogLevel.Error);
                _processName = null;
            }
        }

        public ProcessAudioCapture(string processName)
        {
            _processName = processName;
            Logger.Log($"Creating audio capturer for process: {_processName}", Logger.LogLevel.Debug);
        }

        public void Start()
        {
            if (string.IsNullOrEmpty(_processName))
            {
                Logger.Log("Cannot start - no valid process specified", Logger.LogLevel.Error);
                return;
            }

            Logger.Log($"Starting audio capture for process: {_processName}", Logger.LogLevel.Debug);

            try
            {
                // Create device enumerator
                var deviceEnumerator = new MMDeviceEnumerator();
                var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                // Create WASAPI capture
                _capture = new WasapiLoopbackCapture(device);

                // Subscribe to data available event
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;

                // Start monitoring the target process
                _processMonitorTimer = new System.Timers.Timer(500); // Check every 500ms
                _processMonitorTimer.Elapsed += (s, e) => CheckTargetProcessRunning();
                _processMonitorTimer.Start();
                CheckTargetProcessRunning();

                // Start capturing
                _isCapturing = true;
                _capture.StartRecording();

                Logger.Log("Audio capture started successfully", Logger.LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error starting audio capture: {ex}", Logger.LogLevel.Error);
                CleanUp();
            }
        }

        private void CheckTargetProcessRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(_processName);
                _isTargetProcessRunning = processes.Length > 0;

                if (_isTargetProcessRunning)
                {
                    Logger.Log($"Target process {_processName} is running", Logger.LogLevel.Debug);
                }
                else
                {
                    Logger.Log($"Target process {_processName} is not running", Logger.LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error checking process status: {ex}", Logger.LogLevel.Error);
                _isTargetProcessRunning = false;
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                // Only pass audio data if the target process is running
                if (_isCapturing && _isTargetProcessRunning && e.BytesRecorded > 0)
                {
                    // Create a copy of the data to ensure it's not modified
                    var buffer = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, buffer, e.BytesRecorded);

                    // Raise event
                    DataAvailable?.Invoke(buffer);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in OnDataAvailable: {ex}", Logger.LogLevel.Error);
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            Logger.Log("Recording stopped", Logger.LogLevel.Debug);

            if (e.Exception != null)
            {
                Logger.Log($"Recording stopped with exception: {e.Exception}", Logger.LogLevel.Error);
            }

            _isCapturing = false;
        }

        public void Stop()
        {
            Logger.Log("Stopping audio capture", Logger.LogLevel.Debug);
            _isCapturing = false;

            try
            {
                _processMonitorTimer?.Stop();
                _capture?.StopRecording();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error stopping recording: {ex}", Logger.LogLevel.Error);
            }

            CleanUp();
        }

        private void CleanUp()
        {
            Logger.Log("Cleaning up resources", Logger.LogLevel.Debug);

            try
            {
                if (_processMonitorTimer != null)
                {
                    _processMonitorTimer.Stop();
                    _processMonitorTimer.Dispose();
                    _processMonitorTimer = null;
                }

                if (_capture != null)
                {
                    _capture.DataAvailable -= OnDataAvailable;
                    _capture.RecordingStopped -= OnRecordingStopped;
                    _capture.Dispose();
                    _capture = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error cleaning up resources: {ex}", Logger.LogLevel.Error);
            }
        }

        public void Dispose()
        {
            Logger.Log("Disposing ProcessAudioCapture", Logger.LogLevel.Debug);
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
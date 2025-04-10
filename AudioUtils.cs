// Создаем новый файл AudioUtils.cs
using NAudio.Wave;
using StreamingApplication;
using static System.Windows.Forms.DataFormats;

public static class AudioUtils
{
    public const double SilenceThreshold = 0.0001; // 0.01% от максимальной амплитуды

    // Normalization
    private static float _initialPeak = 0f;
    private static float _referenceGain = 1.0f;
    private static float _referenceTargetLevel = 0.0f;

    public static bool IsSilence(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        if (bytesRecorded == 0) return true;

        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;
        int bytesPerSample = format.BitsPerSample / 8;

        double sum = 0;
        int sampleCount = bytesRecorded / bytesPerSample;

        for (int i = 0; i < bytesRecorded; i += bytesPerSample)
        {
            double sampleValue;
            if (isFloat && bytesPerSample == 4)
            {
                // Для 32-битного float
                sampleValue = BitConverter.ToSingle(buffer, i);
            }
            else
            {
                // Для 16-битного PCM (предполагается по умолчанию)
                short sample = BitConverter.ToInt16(buffer, i);
                sampleValue = sample / (double)short.MaxValue;
            }

            sum += sampleValue * sampleValue;
        }

        double rms = Math.Sqrt(sum / sampleCount);
        Logger.Log($"[WASAPI] RMS: {(float)rms}", Logger.LogLevel.Debug);
        return rms < SilenceThreshold;
    }

    public static void ResetNormalization()
    {
        _initialPeak = 0.0f;
        _referenceGain = 1.0f;
        _referenceTargetLevel = 0.0f;
        Logger.Log("Audio normalization reset", Logger.LogLevel.Debug);
    }

    public static byte[] NormalizeVolume(byte[] audioData, int volumePercent, WaveFormat format)
    {
        float currentPeak = AnalyzePeak(audioData, format);

        // Инициализация при первом запуске
        if (_initialPeak == 0.0f)
        {
            _initialPeak = currentPeak;
            _referenceTargetLevel = volumePercent / 100.0f;
            _referenceGain = CalculateGain(_initialPeak, _referenceTargetLevel, format);
            Logger.Log($"Initial calibration: peak={_initialPeak}, target={_referenceTargetLevel}, gain={_referenceGain}", Logger.LogLevel.Debug);
        }
        else if (currentPeak > 0.0001f) // Не тишина
        {
            // Адаптивный коэффициент - компенсирует изменения входного сигнала
            float adaptiveGain = (_initialPeak / currentPeak) * _referenceGain;
            adaptiveGain = Math.Min(adaptiveGain, 10.0f); // Ограничение максимального усиления

            Logger.Log($"Adaptive gain: {adaptiveGain} (current peak: {currentPeak}, initial: {_initialPeak})", Logger.LogLevel.Debug);
            return ApplyGain(audioData, adaptiveGain, format);
        }

        return ApplyGain(audioData, _referenceGain, format);
    }

    private static float CalculateGain(float peak, float targetLevel, WaveFormat format)
    {
        float maxPossible = (format.BitsPerSample == 32 &&
                             (format.Encoding == WaveFormatEncoding.IeeeFloat ||
                              format.Encoding == WaveFormatEncoding.Extensible))
                           ? 1.0f : 32767f;

        float targetPeak = targetLevel * maxPossible;
        float gain = peak > 0.0001f ? (targetPeak / peak) : 1.0f;
        return Math.Min(gain, 10.0f);
    }

    // Метод для анализа пикового значения
    private static float AnalyzePeak(byte[] audioData, WaveFormat format)
    {
        float currentPeak = 0f;

        if (format.BitsPerSample == 32 &&
           (format.Encoding == WaveFormatEncoding.IeeeFloat ||
            format.Encoding == WaveFormatEncoding.Extensible))
        {
            int sampleCount = audioData.Length / 4;
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = BitConverter.ToSingle(audioData, i * 4);
                currentPeak = Math.Max(currentPeak, Math.Abs(sample));
            }
        }
        else if (format.BitsPerSample == 16)
        {
            int sampleCount = audioData.Length / 2;
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(audioData, i * 2);
                currentPeak = Math.Max(currentPeak, Math.Abs(sample) / 32767f);
            }
        }

        Logger.Log($"Current audio peak: {currentPeak}", Logger.LogLevel.Debug);
        return currentPeak;
    }

    public static byte[] ApplyGain(byte[] audioData, int volumePercent, WaveFormat format)
    {
        var gain = volumePercent / 100f;
        Logger.Log($"Convert volume {volumePercent}% => {gain}f", Logger.LogLevel.Debug);
        return ApplyGain(audioData, gain, format);
    }

    // Метод для применения коэффициента усиления
    public static byte[] ApplyGain(byte[] audioData, float gain, WaveFormat format)
    {
        if (gain == 1.0f || audioData.Length == 0)
        {
            Logger.Log($"Skip applying audio gain: {gain}", Logger.LogLevel.Debug);
            return audioData;
        }

        Logger.Log($"Apply audio gain: {gain}", Logger.LogLevel.Debug);
        byte[] processedData = new byte[audioData.Length];

        if (format.BitsPerSample == 32 &&
           (format.Encoding == WaveFormatEncoding.IeeeFloat ||
            format.Encoding == WaveFormatEncoding.Extensible))
        {
            int sampleCount = audioData.Length / 4;
            for (int i = 0; i < sampleCount; i++)
            {
                int offset = i * 4;
                float sample = BitConverter.ToSingle(audioData, offset) * gain;
                sample = Math.Clamp(sample, -1.0f, 1.0f);
                Buffer.BlockCopy(BitConverter.GetBytes(sample), 0, processedData, offset, 4);
            }
        }
        else if (format.BitsPerSample == 16)
        {
            int sampleCount = audioData.Length / 2;
            for (int i = 0; i < sampleCount; i++)
            {
                int offset = i * 2;
                short sample = (short)(BitConverter.ToInt16(audioData, offset) * gain);
                sample = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
                Buffer.BlockCopy(BitConverter.GetBytes(sample), 0, processedData, offset, 2);
            }
        }

        return processedData;
    }
}
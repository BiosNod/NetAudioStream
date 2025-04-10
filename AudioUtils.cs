using NAudio.Wave;
using StreamingApplication;
using System;
using System.Collections.Generic;
using System.Linq;

public static class AudioUtils
{
    public const double SilenceThreshold = 0.0001; // 0.01% от максимальной амплитуды

    // Простая нормализация на основе среднего пикового значения
    private static Queue<float> _peakHistory = new Queue<float>(20);
    private static float _initialAvgPeak = 0.0f;   // Средний пик начальной калибровки
    private static float _calibrationBaseGain = 1.0f; // Базовая громкость
    private static bool _isCalibrated = false;

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
                sampleValue = BitConverter.ToSingle(buffer, i);
            }
            else
            {
                short sample = BitConverter.ToInt16(buffer, i);
                sampleValue = sample / (double)short.MaxValue;
            }

            sum += sampleValue * sampleValue;
        }

        double rms = Math.Sqrt(sum / sampleCount);
        return rms < SilenceThreshold;
    }

    public static void ResetNormalization()
    {
        _initialAvgPeak = 0.0f;
        _calibrationBaseGain = 1.0f;
        _isCalibrated = false;
        _peakHistory.Clear();
        Logger.Log("Audio normalization reset", Logger.LogLevel.Info);
    }

    public static byte[] NormalizeVolume(byte[] audioData, WaveFormat format)
    {
        // Простое базовое усиление
        float baseGain = 1.0f;

        // Измерение текущего пикового значения в любом случае
        float currentPeak = AnalyzePeak(audioData, format);

        // Обновляем историю пиков, только если не тишина
        if (currentPeak > 0.01f)
        {
            if (_peakHistory.Count >= 20)
                _peakHistory.Dequeue();
            _peakHistory.Enqueue(currentPeak);
        }

        // Если еще не откалибровано
        if (!_isCalibrated)
        {
            // Собираем данные для калибровки
            if (_peakHistory.Count >= 20 && _peakHistory.Average() > 0.01f)
            {
                _initialAvgPeak = _peakHistory.Average();
                _calibrationBaseGain = baseGain;
                _isCalibrated = true;
                Logger.Log($"Normalization calibration complete: _initialAvgPeak={_initialAvgPeak}, _calibrationBaseGain={_calibrationBaseGain}", Logger.LogLevel.Info);
            }
            else
            {
                Logger.Log($"Collecting calibration data: {_peakHistory.Count}/20, currentPeak={currentPeak}", Logger.LogLevel.Debug);
                return ApplyGain(audioData, baseGain, format);
            }
        }

        // Применяем нормализованное усиление
        return ApplyNormalizedGain(audioData, baseGain, format);
    }

    // Главный метод применения нормализованного усиления
    private static byte[] ApplyNormalizedGain(byte[] audioData, float baseGain, WaveFormat format)
    {
        if (!_isCalibrated || _peakHistory.Count == 0)
            return ApplyGain(audioData, baseGain, format);

        // Текущее среднее значение пика
        float currentAvgPeak = _peakHistory.Average();

        // Простая формула: 
        // 1. Сначала определяем, насколько текущий avg отличается от начального
        // 2. Затем корректируем текущее усиление для возврата к такому же уровню громкости
        float ratio = (_initialAvgPeak / currentAvgPeak);

        // Корректируем с учетом изменения громкости пользователем
        float volumeRatio = baseGain / _calibrationBaseGain;

        // Вычисляем итоговое усиление
        float adjustedGain = baseGain * ratio;

        Logger.Log($"Normalization: currentAvgPeak={currentAvgPeak}, initialAvgPeak={_initialAvgPeak}, " +
                  $"ratio={ratio}, baseGain={baseGain}, adjustedGain={adjustedGain}", Logger.LogLevel.Debug);

        return ApplyGain(audioData, adjustedGain, format);
    }

    // Анализ пикового значения
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

        return currentPeak;
    }

    public static byte[] ApplyGain(byte[] audioData, int volumePercent, WaveFormat format)
    {
        var gain = volumePercent / 100f;
        return ApplyGain(audioData, gain, format);
    }

    public static byte[] ApplyGain(byte[] audioData, float gain, WaveFormat format)
    {
        if (Math.Abs(gain - 1.0f) < 0.01f || audioData.Length == 0)
            return audioData;

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
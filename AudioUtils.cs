// Создаем новый файл AudioUtils.cs
using NAudio.Wave;
using StreamingApplication;

public static class AudioUtils
{
    public const double SilenceThreshold = 0.0001; // 0.01% от максимальной амплитуды

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
        Logger.Log($"[WASAPI] RMS: {rms}", Logger.LogLevel.Debug);
        return rms < SilenceThreshold;
    }
}
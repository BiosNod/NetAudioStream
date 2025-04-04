// Logger.cs
using System;

namespace StreamingApplication
{
    public static class Logger
    {
        public enum LogLevel { Debug, Info, Warning, Error }

        public static bool DebugEnabled { get; set; } = false;

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (level == LogLevel.Debug && !DebugEnabled)
                return;

            var color = level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => ConsoleColor.White
            };

            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            Console.ResetColor();
        }
    }
}
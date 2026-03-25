using System;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    /// <summary>
    /// Implementation of ILogger that outputs to the console only.
    /// File persistence is handled by FileLogger via CompositeLogger.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private static readonly object _lock = new object();

        public void Info(string message)
        {
            string logLine = $"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
            WriteWithColor(logLine, ConsoleColor.White);
        }

        public void Debug(string message)
        {
#if DEBUG
            string logLine = $"[DEBUG] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
            WriteWithColor(logLine, ConsoleColor.Gray);
#endif
        }

        public void Warn(string message)
        {
            string logLine = $"[WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
            WriteWithColor(logLine, ConsoleColor.Yellow);
        }

        public void Error(string message, Exception ex = null)
        {
            string logLine = $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
            WriteWithColor(logLine, ConsoleColor.Red);
            
            if (ex != null)
            {
                WriteWithColor($"Exception: {ex.Message}", ConsoleColor.Red);
                WriteWithColor($"StackTrace: {ex.StackTrace}", ConsoleColor.Red);
            }
        }

        public void Error(string message)
        {
            Error(message, null);
        }

        private static void WriteWithColor(string message, ConsoleColor color)
        {
            lock (_lock)
            {
                var originalColor = Console.ForegroundColor;
                try
                {
                    Console.ForegroundColor = color;
                    Console.WriteLine(message);
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }
            }
        }
    }
}

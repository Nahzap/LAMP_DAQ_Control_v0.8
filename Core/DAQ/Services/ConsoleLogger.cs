using System;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    /// <summary>
    /// Implementation of ILogger that outputs to the console
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private static readonly object _lock = new object();

        public void Info(string message)
        {
            WriteWithColor($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}", ConsoleColor.White);
        }

        public void Debug(string message)
        {
#if DEBUG
            WriteWithColor($"[DEBUG] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}", ConsoleColor.Gray);
#endif
        }

        public void Warn(string message)
        {
            WriteWithColor($"[WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}", ConsoleColor.Yellow);
        }

        public void Error(string message, Exception ex = null)
        {
            var errorMessage = $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
            if (ex != null)
            {
                errorMessage += $"\n{ex}";
            }
            WriteWithColor(errorMessage, ConsoleColor.Red);
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

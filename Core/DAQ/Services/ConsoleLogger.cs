using System;
using System.IO;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    /// <summary>
    /// Implementation of ILogger that outputs to the console
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private static readonly object _lock = new object();
        private static readonly object _fileLock = new object();
        private readonly string _logFilePath;

        public ConsoleLogger()
        {
            // Log file in application root directory
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            _logFilePath = Path.Combine(appDir, "LATEST_LOG.txt");
            
            // Clear/create log file on startup
            try
            {
                lock (_fileLock)
                {
                    File.WriteAllText(_logFilePath, 
                        $"LAMP DAQ Control v0.8 - Session Log\n" +
                        $"Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                        $"========================================\n\n");
                }
            }
            catch { /* Ignore file errors */ }
        }

        public void Info(string message)
        {
            string logLine = $"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
            WriteWithColor(logLine, ConsoleColor.White);
            WriteToFile(logLine);
        }

        public void Debug(string message)
        {
#if DEBUG
            string logLine = $"[DEBUG] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
            WriteWithColor(logLine, ConsoleColor.Gray);
            WriteToFile(logLine);
#endif
        }

        public void Warn(string message)
        {
            string logLine = $"[WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
            WriteWithColor(logLine, ConsoleColor.Yellow);
            WriteToFile(logLine);
        }

        public void Error(string message, Exception ex = null)
        {
            string logLine = $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}";
            WriteWithColor(logLine, ConsoleColor.Red);
            WriteToFile(logLine);
            
            if (ex != null)
            {
                string exLine = $"Exception: {ex.Message}";
                WriteWithColor(exLine, ConsoleColor.Red);
                WriteToFile(exLine);
                
                string stackLine = $"StackTrace: {ex.StackTrace}";
                WriteWithColor(stackLine, ConsoleColor.Red);
                WriteToFile(stackLine);
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

        private void WriteToFile(string line)
        {
            try
            {
                lock (_fileLock)
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch { /* Ignore file write errors */ }
        }
    }
}

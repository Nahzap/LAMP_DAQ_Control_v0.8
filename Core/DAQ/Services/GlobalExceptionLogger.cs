using System;
using System.IO;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    /// <summary>
    /// Global exception logger that captures all unhandled exceptions and critical errors.
    /// This is a singleton that ensures all exceptions are persisted to disk even if the UI crashes.
    /// </summary>
    public static class GlobalExceptionLogger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath;
        private static ILogger _logger;
        private static bool _isInitialized = false;

        /// <summary>
        /// Initialize the global exception logger with a logger instance
        /// </summary>
        public static void Initialize(ILogger logger)
        {
            lock (_lock)
            {
                if (_isInitialized)
                    return;

                _logger = logger;
                
                // Create emergency log file path
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string logsDir = Path.Combine(appDir, "Logs");
                
                try
                {
                    if (!Directory.Exists(logsDir))
                        Directory.CreateDirectory(logsDir);
                }
                catch { /* Ignore directory creation errors */ }
                
                _logFilePath = Path.Combine(logsDir, $"CRITICAL_ERRORS_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                
                _isInitialized = true;
                
                LogInfo("GlobalExceptionLogger initialized");
            }
        }

        /// <summary>
        /// Log an unhandled exception with full context
        /// </summary>
        public static void LogUnhandledException(Exception ex, string context = "Unknown")
        {
            lock (_lock)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string message = $"[UNHANDLED EXCEPTION] Context: {context}\n" +
                                $"Type: {ex.GetType().FullName}\n" +
                                $"Message: {ex.Message}\n" +
                                $"StackTrace:\n{ex.StackTrace}\n";
                
                if (ex.InnerException != null)
                {
                    message += $"\nInner Exception:\n" +
                              $"Type: {ex.InnerException.GetType().FullName}\n" +
                              $"Message: {ex.InnerException.Message}\n" +
                              $"StackTrace:\n{ex.InnerException.StackTrace}\n";
                }
                
                // Log to logger if available
                _logger?.Error($"UNHANDLED EXCEPTION in {context}", ex);
                
                // Always write to emergency file
                WriteToEmergencyLog(timestamp, message);
            }
        }

        /// <summary>
        /// Log a critical error that may cause application instability
        /// </summary>
        public static void LogCriticalError(string message, Exception ex = null)
        {
            lock (_lock)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string fullMessage = $"[CRITICAL ERROR] {message}";
                
                if (ex != null)
                {
                    fullMessage += $"\nException: {ex.GetType().FullName}\n" +
                                  $"Message: {ex.Message}\n" +
                                  $"StackTrace:\n{ex.StackTrace}";
                }
                
                // Log to logger if available
                if (ex != null)
                    _logger?.Error(message, ex);
                else
                    _logger?.Error(message);
                
                // Always write to emergency file
                WriteToEmergencyLog(timestamp, fullMessage);
            }
        }

        /// <summary>
        /// Log informational message
        /// </summary>
        public static void LogInfo(string message)
        {
            lock (_lock)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _logger?.Info(message);
                WriteToEmergencyLog(timestamp, $"[INFO] {message}");
            }
        }

        /// <summary>
        /// Log warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            lock (_lock)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _logger?.Warn(message);
                WriteToEmergencyLog(timestamp, $"[WARNING] {message}");
            }
        }

        private static void WriteToEmergencyLog(string timestamp, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_logFilePath))
                    return;

                string logEntry = $"{timestamp} {message}\n" +
                                 $"{new string('=', 80)}\n\n";
                
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch
            {
                // If we can't write to the emergency log, there's nothing we can do
                // This is the last resort logging mechanism
            }
        }

        /// <summary>
        /// Get the path to the emergency log file
        /// </summary>
        public static string GetEmergencyLogPath()
        {
            return _logFilePath;
        }
    }
}

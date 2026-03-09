using System;
using System.IO;
using System.Text;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    /// <summary>
    /// Logger que escribe a archivo con rotación automática
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly string _logFilePrefix;
        private readonly object _lock = new object();
        private readonly long _maxFileSizeBytes;
        private string _currentLogFile;

        public FileLogger(string logDirectory = null, string logFilePrefix = "LAMP_DAQ", long maxFileSizeMB = 10)
        {
            _logDirectory = logDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LAMP_DAQ_Control",
                "Logs"
            );
            
            _logFilePrefix = logFilePrefix;
            _maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;

            // Crear directorio si no existe
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            InitializeLogFile();
        }

        private void InitializeLogFile()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentLogFile = Path.Combine(_logDirectory, $"{_logFilePrefix}_{timestamp}.log");
            
            // Escribir encabezado
            WriteToFile($"========================================");
            WriteToFile($"LAMP DAQ Control - Log Session Started");
            WriteToFile($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            WriteToFile($"Machine: {Environment.MachineName}");
            WriteToFile($"User: {Environment.UserName}");
            WriteToFile($"OS: {Environment.OSVersion}");
            WriteToFile($"========================================");
        }

        private void CheckFileRotation()
        {
            // NO ROTATION DURING SESSION
            // Each session gets its own log file created at startup
            // All messages from the session are kept in the same file
            // File only rotates if it exceeds a very large size (100MB) to prevent disk issues
            if (File.Exists(_currentLogFile))
            {
                var fileInfo = new FileInfo(_currentLogFile);
                if (fileInfo.Length >= 100 * 1024 * 1024) // 100MB emergency limit
                {
                    WriteToFile("========================================");
                    WriteToFile("EMERGENCY: Log file size limit reached (100MB). Rotating...");
                    WriteToFile("========================================");
                    InitializeLogFile();
                }
            }
        }

        private void WriteToFile(string message)
        {
            try
            {
                File.AppendAllText(_currentLogFile, message + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // Fallback a consola si falla escritura a archivo
                Console.WriteLine($"[FileLogger Error] {ex.Message}");
                Console.WriteLine(message);
            }
        }

        private void WriteLog(string level, string message, Exception ex = null)
        {
            lock (_lock)
            {
                CheckFileRotation();

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logMessage = $"[{timestamp}] [{level}] {message}";

                WriteToFile(logMessage);

                if (ex != null)
                {
                    WriteToFile($"  Exception: {ex.GetType().Name}");
                    WriteToFile($"  Message: {ex.Message}");
                    WriteToFile($"  StackTrace: {ex.StackTrace}");
                    
                    if (ex.InnerException != null)
                    {
                        WriteToFile($"  InnerException: {ex.InnerException.Message}");
                    }
                }
            }
        }

        public void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public void Debug(string message)
        {
#if DEBUG
            WriteLog("DEBUG", message);
#endif
        }

        public void Warn(string message)
        {
            WriteLog("WARN", message);
        }

        public void Error(string message, Exception ex = null)
        {
            WriteLog("ERROR", message, ex);
        }

        public void Error(string message)
        {
            Error(message, null);
        }

        /// <summary>
        /// Obtiene la ruta del archivo de log actual
        /// </summary>
        public string GetCurrentLogFilePath()
        {
            return _currentLogFile;
        }

        /// <summary>
        /// Limpia logs antiguos (más de X días)
        /// </summary>
        public void CleanOldLogs(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(_logDirectory, $"{_logFilePrefix}_*.log");

                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                        Info($"Deleted old log file: {Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Error($"Error cleaning old logs: {ex.Message}", ex);
            }
        }
    }
}

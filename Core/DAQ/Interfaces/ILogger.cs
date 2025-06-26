using System;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces
{
    /// <summary>
    /// Defines a logging interface for the application
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs an informational message
        /// </summary>
        void Info(string message);

        /// <summary>
        /// Logs a debug message
        /// </summary>
        void Debug(string message);

        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        void Warn(string message);
        
        /// <summary>
        /// Logs an error message with optional exception
        /// </summary>
        void Error(string message, Exception ex = null);

        /// <summary>
        /// Logs an error message
        /// </summary>
        void Error(string message);
    }
}

using System;
using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    /// <summary>
    /// Logger que combina múltiples loggers (Console + File + UI)
    /// </summary>
    public class CompositeLogger : ILogger
    {
        private readonly List<ILogger> _loggers;
        private readonly object _lock = new object();

        public CompositeLogger(params ILogger[] loggers)
        {
            _loggers = new List<ILogger>(loggers ?? new ILogger[0]);
        }

        public void AddLogger(ILogger logger)
        {
            if (logger != null)
            {
                lock (_lock)
                {
                    _loggers.Add(logger);
                }
            }
        }

        public void RemoveLogger(ILogger logger)
        {
            if (logger != null)
            {
                lock (_lock)
                {
                    _loggers.Remove(logger);
                }
            }
        }

        public void Info(string message)
        {
            lock (_lock)
            {
                foreach (var logger in _loggers)
                {
                    try
                    {
                        logger.Info(message);
                    }
                    catch
                    {
                        // Ignorar errores en loggers individuales
                    }
                }
            }
        }

        public void Debug(string message)
        {
            lock (_lock)
            {
                foreach (var logger in _loggers)
                {
                    try
                    {
                        logger.Debug(message);
                    }
                    catch
                    {
                        // Ignorar errores en loggers individuales
                    }
                }
            }
        }

        public void Warn(string message)
        {
            lock (_lock)
            {
                foreach (var logger in _loggers)
                {
                    try
                    {
                        logger.Warn(message);
                    }
                    catch
                    {
                        // Ignorar errores en loggers individuales
                    }
                }
            }
        }

        public void Error(string message, Exception ex = null)
        {
            lock (_lock)
            {
                foreach (var logger in _loggers)
                {
                    try
                    {
                        logger.Error(message, ex);
                    }
                    catch
                    {
                        // Ignorar errores en loggers individuales
                    }
                }
            }
        }

        public void Error(string message)
        {
            Error(message, null);
        }
    }
}

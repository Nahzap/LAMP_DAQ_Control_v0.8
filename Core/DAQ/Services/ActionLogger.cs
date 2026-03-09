using System;
using System.Diagnostics;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    /// <summary>
    /// Logger especializado para acciones de usuario y operaciones del sistema
    /// </summary>
    public class ActionLogger
    {
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch;

        public ActionLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stopwatch = new Stopwatch();
        }

        /// <summary>
        /// Registra una acción de usuario en la UI
        /// </summary>
        public void LogUserAction(string actionName, string details = null)
        {
            var message = $"[USER ACTION] {actionName}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | Details: {details}";
            }
            _logger.Info(message);
        }

        /// <summary>
        /// Registra un click de botón
        /// </summary>
        public void LogButtonClick(string buttonName, string viewModel = null)
        {
            var message = $"[BUTTON CLICK] {buttonName}";
            if (!string.IsNullOrEmpty(viewModel))
            {
                message += $" | ViewModel: {viewModel}";
            }
            _logger.Info(message);
        }

        /// <summary>
        /// Registra un cambio de valor en la UI
        /// </summary>
        public void LogValueChange(string propertyName, object oldValue, object newValue, string source = null)
        {
            var message = $"[VALUE CHANGE] {propertyName}: {oldValue} → {newValue}";
            if (!string.IsNullOrEmpty(source))
            {
                message += $" | Source: {source}";
            }
            _logger.Info(message);
        }

        /// <summary>
        /// Registra una operación de hardware
        /// </summary>
        public void LogHardwareOperation(string operation, string details = null)
        {
            var message = $"[HARDWARE] {operation}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | {details}";
            }
            _logger.Info(message);
        }

        /// <summary>
        /// Registra escritura analógica
        /// </summary>
        public void LogAnalogWrite(int channel, double voltage, bool success = true)
        {
            var status = success ? "SUCCESS" : "FAILED";
            _logger.Info($"[ANALOG WRITE] Channel={channel}, Voltage={voltage:F3}V | Status: {status}");
        }

        /// <summary>
        /// Registra escritura digital
        /// </summary>
        public void LogDigitalWrite(int port, int? bit, byte value, bool success = true)
        {
            var status = success ? "SUCCESS" : "FAILED";
            var bitInfo = bit.HasValue ? $", Bit={bit.Value}" : "";
            _logger.Info($"[DIGITAL WRITE] Port={port}{bitInfo}, Value=0x{value:X2} | Status: {status}");
        }

        /// <summary>
        /// Registra lectura digital
        /// </summary>
        public void LogDigitalRead(int port, int? bit, byte value, bool success = true)
        {
            var status = success ? "SUCCESS" : "FAILED";
            var bitInfo = bit.HasValue ? $", Bit={bit.Value}" : "";
            _logger.Info($"[DIGITAL READ] Port={port}{bitInfo}, Value=0x{value:X2} | Status: {status}");
        }

        /// <summary>
        /// Registra inicio de generación de señal
        /// </summary>
        public void LogSignalStart(int channel, double frequency, double amplitude, double offset)
        {
            _logger.Info($"[SIGNAL START] Channel={channel}, Freq={frequency}Hz, Amp={amplitude:F2}V, Offset={offset:F2}V");
        }

        /// <summary>
        /// Registra detención de señal
        /// </summary>
        public void LogSignalStop(int? channel = null)
        {
            var channelInfo = channel.HasValue ? $"Channel={channel.Value}" : "All channels";
            _logger.Info($"[SIGNAL STOP] {channelInfo}");
        }

        /// <summary>
        /// Registra inicio de rampa
        /// </summary>
        public void LogRampStart(int channel, double startValue, double targetValue, int durationMs)
        {
            _logger.Info($"[RAMP START] Channel={channel}, {startValue:F2}V → {targetValue:F2}V, Duration={durationMs}ms");
        }

        /// <summary>
        /// Registra fin de rampa
        /// </summary>
        public void LogRampEnd(int channel, double finalValue, long actualDurationMs)
        {
            _logger.Info($"[RAMP END] Channel={channel}, FinalValue={finalValue:F2}V, ActualDuration={actualDurationMs}ms");
        }

        /// <summary>
        /// Registra detección de dispositivos
        /// </summary>
        public void LogDeviceDetection(int deviceCount, string details = null)
        {
            var message = $"[DEVICE DETECTION] Found {deviceCount} device(s)";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | {details}";
            }
            _logger.Info(message);
        }

        /// <summary>
        /// Registra inicialización de dispositivo
        /// </summary>
        public void LogDeviceInitialization(string deviceName, int deviceNumber, bool success, string errorMessage = null)
        {
            var status = success ? "SUCCESS" : "FAILED";
            var message = $"[DEVICE INIT] {deviceName} (ID={deviceNumber}) | Status: {status}";
            
            if (!success && !string.IsNullOrEmpty(errorMessage))
            {
                message += $" | Error: {errorMessage}";
            }

            if (success)
            {
                _logger.Info(message);
            }
            else
            {
                _logger.Error(message);
            }
        }

        /// <summary>
        /// Registra cambio de perfil
        /// </summary>
        public void LogProfileChange(string oldProfile, string newProfile)
        {
            _logger.Info($"[PROFILE CHANGE] {oldProfile ?? "None"} → {newProfile}");
        }

        /// <summary>
        /// Registra inicio de monitoreo digital
        /// </summary>
        public void LogMonitoringStart(int deviceNumber, int intervalMs)
        {
            _logger.Info($"[MONITORING START] Device={deviceNumber}, Interval={intervalMs}ms");
        }

        /// <summary>
        /// Registra detención de monitoreo
        /// </summary>
        public void LogMonitoringStop()
        {
            _logger.Info($"[MONITORING STOP]");
        }

        /// <summary>
        /// Inicia medición de tiempo para una operación
        /// </summary>
        public void StartTiming()
        {
            _stopwatch.Restart();
        }

        /// <summary>
        /// Detiene medición de tiempo y registra
        /// </summary>
        public void StopTiming(string operationName)
        {
            _stopwatch.Stop();
            _logger.Info($"[TIMING] {operationName} completed in {_stopwatch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Registra excepción con contexto
        /// </summary>
        public void LogException(string context, Exception ex)
        {
            _logger.Error($"[EXCEPTION] Context: {context}", ex);
        }

        /// <summary>
        /// Registra advertencia
        /// </summary>
        public void LogWarning(string message, string context = null)
        {
            var fullMessage = context != null ? $"[{context}] {message}" : message;
            _logger.Warn(fullMessage);
        }

        /// <summary>
        /// Registra cambio de estado de la aplicación
        /// </summary>
        public void LogStateChange(string stateName, string oldState, string newState)
        {
            _logger.Info($"[STATE CHANGE] {stateName}: {oldState} → {newState}");
        }

        /// <summary>
        /// Registra navegación en la UI
        /// </summary>
        public void LogNavigation(string from, string to)
        {
            _logger.Info($"[NAVIGATION] {from} → {to}");
        }

        /// <summary>
        /// Registra apertura/cierre de ventana
        /// </summary>
        public void LogWindowEvent(string windowName, bool isOpening)
        {
            var action = isOpening ? "OPENED" : "CLOSED";
            _logger.Info($"[WINDOW {action}] {windowName}");
        }
    }
}

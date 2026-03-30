using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Exceptions;
using LAMP_DAQ_Control_v0_8.Core.DAQ.HAL;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Managers;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Engine;
using System.Collections.ObjectModel;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ
{
    /// <summary>
    /// Controls Advantech DAQ devices with support for multiple channels and profiles
    /// </summary>
    public class DAQController : IDisposable
    {
        #region Fields
        private readonly IDeviceManager _deviceManager;
        private readonly DeviceManager _concreteDeviceManager; // HIGH-01: Typed access for SDK handles
        private readonly IProfileManager _profileManager;
        private readonly IChannelManager _channelManager;
        private ISignalGenerator _signalGenerator; // Not readonly - recreated on device switch
        private readonly ILogger _logger;
        private DaqEngine _engine; // High-performance parallel engine (optional)
        private AdvantechAnalogHal _sharedAnalogHal;   // CRIT-01: Single HAL shared by SignalGenerator + DaqEngine
        private AdvantechDigitalHal _sharedDigitalHal;  // CRIT-01: Single HAL shared by DaqEngine
        private bool _disposed;
        #endregion
        
        #region Constructor & Factory Methods
        /// <summary>
        /// Initializes a new instance of the DAQController class
        /// </summary>
        /// <param name="logger">Optional logger instance</param>
        /// <param name="deviceManager">Optional device manager instance</param>
        /// <param name="profileManager">Optional profile manager instance</param>
        /// <param name="channelManager">Optional channel manager instance</param>
        /// <param name="signalGenerator">Optional signal generator instance</param>
        public DAQController(
            ILogger logger = null, 
            IDeviceManager deviceManager = null, 
            IProfileManager profileManager = null, 
            IChannelManager channelManager = null, 
            ISignalGenerator signalGenerator = null)
        {
            try
            {
                _logger = logger ?? new ConsoleLogger();
                
                // Initialize device manager
                if (deviceManager != null)
                {
                    _deviceManager = deviceManager;
                    _concreteDeviceManager = deviceManager as DeviceManager;
                }
                else
                {
                    var dm = new DeviceManager(_logger);
                    _deviceManager = dm;
                    _concreteDeviceManager = dm;
                }
                
                // Initialize signal generator if not provided
                // CRIT-03: SignalGenerator now receives IAnalogHal instead of raw InstantAoCtrl
                if (signalGenerator != null)
                {
                    _signalGenerator = signalGenerator;
                }
                else if (_concreteDeviceManager != null)
                {
                    _sharedAnalogHal = new AdvantechAnalogHal(_logger);
                    _sharedAnalogHal.InitializeFromExisting(_concreteDeviceManager.Device);
                    _signalGenerator = new SignalGenerator(_sharedAnalogHal, _logger);
                }
                else
                {
                    // Fallback: create a dummy signal generator
                    _sharedAnalogHal = new AdvantechAnalogHal(_logger);
                    _signalGenerator = new SignalGenerator(_sharedAnalogHal, _logger);
                }
                
                // Initialize profile manager
                _profileManager = profileManager ?? new ProfileManager(_deviceManager, _logger);
                
                // Initialize channel manager
                _channelManager = channelManager ?? new ChannelManager(_deviceManager, _logger);
                
                _logger.Info("DAQ controller created successfully");
            }
            catch (Exception ex)
            {
                const string errorMsg = "Error creating DAQ controller";
                _logger.Error(errorMsg, ex);
                throw new DAQInitializationException(errorMsg, ex);
            }
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// Gets a value indicating whether the controller is initialized and ready for use
        /// </summary>
        public bool IsInitialized => _deviceManager.IsInitialized && !_disposed;

        /// <summary>
        /// Gets the number of available channels on the device
        /// </summary>
        public int ChannelCount => _deviceManager.ChannelCount;
        
        /// <summary>
        /// Gets the detected device model
        /// </summary>
        public string DeviceModel => _deviceManager.DeviceModel;
        
        /// <summary>
        /// Gets the active profile name, if any
        /// </summary>
        public string ActiveProfileName => _profileManager.ActiveProfileName;
        
        /// <summary>
        /// Gets a list of available profile names
        /// </summary>
        public IReadOnlyCollection<string> AvailableProfiles => _profileManager.AvailableProfiles;

        /// <summary>
        /// Gets the high-performance engine instance (null if not started).
        /// Used by ExecutionEngine and ViewModels for parallel operations.
        /// </summary>
        public DaqEngine Engine => _engine;

        /// <summary>
        /// Whether the high-performance engine is running.
        /// </summary>
        public bool IsEngineRunning => _engine != null && _engine.IsRunning;
        #endregion

        #region Public Methods
        /// <summary>
        /// Initializes the DAQ controller with the specified configuration
        /// </summary>
        /// <param name="profilePath">Optional path to the configuration profile file</param>
        /// <param name="deviceNumber">Device number to initialize (default: 0)</param>
        /// <exception cref="ObjectDisposedException">If the controller is disposed</exception>
        /// <exception cref="DAQInitializationException">If device initialization fails</exception>
        public void Initialize(string profileName = null, int deviceNumber = 0)
        {
            EnsureNotDisposed();

            try
            {
                // Determinar tipo de dispositivo objetivo (fuente de verdad única)
                DeviceType targetType = DeviceTypeResolver.ResolveFromProfile(profileName);
                
                // CRITICAL FIX: Solo bloquear si es el MISMO tipo de dispositivo
                if (_deviceManager.IsInitialized && _deviceManager.CurrentDeviceType == targetType)
                {
                    _logger.Info($"Device is already initialized (Type: {_deviceManager.CurrentDeviceType}, Target: {targetType})");
                    return;
                }
                
                _logger.Info($"Inicializando dispositivo {deviceNumber} con perfil: {profileName ?? "<ninguno>"}" +
                             $" (Tipo objetivo: {targetType})");

                // Initialize the device with profile name to help determine device type
                _deviceManager.InitializeDevice(deviceNumber, profileName);
                
                // CRIT-01/03: Recreate shared HAL and SignalGenerator after device switch
                // HAL wraps DeviceManager's SDK instance — single owner, no duplication
                if (_sharedAnalogHal != null)
                {
                    _sharedAnalogHal.Dispose();
                }
                _sharedAnalogHal = new AdvantechAnalogHal(_logger);
                // HIGH-01: Use concrete DeviceManager for SDK handle access
                if (_concreteDeviceManager != null)
                {
                    _sharedAnalogHal.InitializeFromExisting(_concreteDeviceManager.Device);
                }
                _signalGenerator = new SignalGenerator(_sharedAnalogHal, _logger);
                _logger.Info("Shared AnalogHAL + SignalGenerator recreated with device instance");

                // Also create/refresh digital HAL if device is digital
                if (_deviceManager.CurrentDeviceType == DeviceType.Digital && _concreteDeviceManager != null)
                {
                    if (_sharedDigitalHal != null)
                    {
                        _sharedDigitalHal.Dispose();
                    }
                    _sharedDigitalHal = new AdvantechDigitalHal(_logger);
                    // HIGH-01: Use concrete DeviceManager for SDK handle access
                    _sharedDigitalHal.InitializeFromExisting(
                        _concreteDeviceManager.DigitalInputDevice, 
                        _concreteDeviceManager.DigitalOutputDevice);
                    _logger.Info("Shared DigitalHAL created from DeviceManager instances");
                }
                
                // Verificar que el perfil sea compatible con el tipo de dispositivo detectado
                var deviceInfo = _deviceManager.GetDeviceInfo();
                _logger.Info($"Dispositivo detectado: {deviceInfo.Name}, Tipo: {deviceInfo.DeviceType}");
                
                // Corregir el perfil si es necesario
                string correctedProfile = profileName;
                if (deviceInfo.DeviceType != targetType && targetType != DeviceType.Unknown)
                {
                    correctedProfile = DeviceTypeResolver.GetDefaultProfile(deviceInfo.DeviceType);
                    if (correctedProfile != null)
                        _logger.Warn($"Perfil incompatible. Cambiando a: {correctedProfile}");
                }
                else if (targetType == DeviceType.Unknown)
                {
                    correctedProfile = DeviceTypeResolver.GetDefaultProfile(deviceInfo.DeviceType) ?? profileName;
                }
                
                // Try to load the profile (ahora solo necesitamos llamar a un método)
                try
                {
                    _profileManager.TryLoadProfile(correctedProfile);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"No se pudo cargar el perfil: {ex.Message}");
                    // Configure default channels if no profile is loaded
                    _deviceManager.ConfigureChannels(_profileManager.GetDefaultRange());
                }

                _logger.Info("Device initialization completed successfully");
            }
            catch (Exception ex) when (!(ex is DAQInitializationException))
            {
                string errorMsg = "Error during DAQ device initialization";
                _logger.Error(errorMsg, ex);
                throw new DAQInitializationException(errorMsg, ex);
            }
        }

        /// <summary>
        /// Writes a voltage value to the specified channel
        /// </summary>
        /// <param name="channel">Channel number (0-based)</param>
        /// <param name="value">Value to write (in Volts)</param>
        /// <exception cref="ObjectDisposedException">If the controller is disposed</exception>
        /// <exception cref="InvalidOperationException">If device is not initialized</exception>
        /// <exception cref="ArgumentOutOfRangeException">If channel is out of range</exception>
        public void WriteVoltage(int channel, double value)
        {
            EnsureNotDisposed();
            _channelManager.WriteVoltage(channel, value);
        }

        /// <summary>
        /// Scans for available DAQ devices
        /// </summary>
        /// <returns>List of detected devices</returns>
        public IList<DeviceInfo> DetectDevices()
        {
            EnsureNotDisposed();
            return _deviceManager.DetectDevices();
        }

        /// <summary>
        /// Gets information about the current device
        /// </summary>
        public DeviceInfo GetDeviceInfo()
        {
            EnsureNotDisposed();
            return _deviceManager.GetDeviceInfo();
        }
        #endregion

        #region Channel Information Methods
        
        /// <summary>
        /// Gets the current state of all channels
        /// </summary>
        /// <returns>Collection of channel states</returns>
        public IReadOnlyCollection<ChannelState> GetChannelStates()
        {
            EnsureNotDisposed();
            return _channelManager.GetChannelStates(_signalGenerator);
        }
        
        #endregion
        
        #region Signal Generation Methods
        /// <summary>
        /// Sets a DC value on the specified channel
        /// </summary>
        public void SetChannelValue(int channel, double value)
        {
            EnsureNotDisposed();
            _signalGenerator.SetDcValue(channel, value);
        }
        
        /// <summary>
        /// Ramps a channel to a target value over the specified duration
        /// </summary>
        public Task RampChannelValue(int channel, double targetValue, int durationMs)
        {
            EnsureNotDisposed();
            return _signalGenerator.SetDcValueAsync(channel, targetValue, durationMs);
        }
        
        /// <summary>
        /// Resets all channels to their default values (0V or 4mA)
        /// </summary>
        public void ResetAllChannels()
        {
            EnsureNotDisposed();
            _channelManager.ResetAllChannels();
        }

        /// <summary>
        /// Starts signal generation on the specified channel
        /// </summary>
        public void StartSignalGeneration(int channel, double frequency, double amplitude, double offset)
        {
            EnsureNotDisposed();
            _signalGenerator.Start(channel, frequency, amplitude, offset);
        }

        /// <summary>
        /// Stops any active signal generation
        /// </summary>
        public void StopSignalGeneration()
        {
            if (!_disposed)
            {
                _signalGenerator.Stop();
            }
        }
        
        /// <summary>
        /// Gets the signal generator for granular control (e.g., stopping specific channels)
        /// </summary>
        public ISignalGenerator GetSignalGenerator()
        {
            EnsureNotDisposed();
            return _signalGenerator;
        }
        #endregion
        
        #region Digital I/O Methods
        /// <summary>
        /// Escribe un valor en un puerto digital completo.
        /// Routes through DaqEngine pipeline if running, otherwise direct write.
        /// </summary>
        /// <param name="port">Número de puerto (0-3)</param>
        /// <param name="value">Valor a escribir (0-255)</param>
        public void WriteDigitalPort(int port, byte value)
        {
            EnsureNotDisposed();
            ValidatePortNumber(port);
            if (_engine != null && _engine.IsRunning && _engine.HasDigital)
                _engine.WriteDigitalPort(port, value);
            else
                _deviceManager.WriteDigitalPort(port, value);
        }
        
        /// <summary>
        /// Lee el valor de un puerto digital completo.
        /// Uses StateGrid cache if engine running, otherwise direct read.
        /// </summary>
        /// <param name="port">Número de puerto (0-3)</param>
        /// <returns>Valor del puerto (0-255)</returns>
        public byte ReadDigitalPort(int port)
        {
            EnsureNotDisposed();
            ValidatePortNumber(port);
            if (_engine != null && _engine.IsRunning && _engine.HasDigital)
                return _engine.ReadDigitalPort(port);
            return _deviceManager.ReadDigitalPort(port);
        }
        
        /// <summary>
        /// Escribe un valor en un bit específico de un puerto digital.
        /// Routes through DaqEngine pipeline if running.
        /// </summary>
        /// <param name="port">Número de puerto (0-3)</param>
        /// <param name="bit">Número de bit (0-7)</param>
        /// <param name="value">Valor a escribir (true=1, false=0)</param>
        public void WriteDigitalBit(int port, int bit, bool value)
        {
            EnsureNotDisposed();
            ValidatePortNumber(port);
            ValidateBitNumber(bit);
            if (_engine != null && _engine.IsRunning && _engine.HasDigital)
                _engine.WriteDigitalBit(port, bit, value);
            else
                _deviceManager.WriteDigitalBit(port, bit, value);
        }
        
        /// <summary>
        /// Lee el valor de un bit específico de un puerto digital.
        /// Uses StateGrid cache if engine running.
        /// </summary>
        /// <param name="port">Número de puerto (0-3)</param>
        /// <param name="bit">Número de bit (0-7)</param>
        /// <returns>Valor del bit (true=1, false=0)</returns>
        public bool ReadDigitalBit(int port, int bit)
        {
            EnsureNotDisposed();
            ValidatePortNumber(port);
            ValidateBitNumber(bit);
            if (_engine != null && _engine.IsRunning && _engine.HasDigital)
                return _engine.ReadDigitalBit(port, bit);
            return _deviceManager.ReadDigitalBit(port, bit);
        }
        #endregion

        #region Engine Lifecycle
        /// <summary>
        /// Creates and starts the high-performance DaqEngine.
        /// Enables parallel digital+analog operations through the pipeline.
        /// Call after Initialize() for both device types.
        /// </summary>
        /// <param name="digitalDeviceNumber">Board ID for digital device (-1 to skip)</param>
        /// <param name="analogDeviceNumber">Board ID for analog device (-1 to skip)</param>
        /// <param name="outputIntervalUs">Output cycle interval in microseconds (default 500 = 2kHz)</param>
        public void StartEngine(int digitalDeviceNumber = -1, int analogDeviceNumber = -1, int outputIntervalUs = 500)
        {
            EnsureNotDisposed();

            if (_engine != null && _engine.IsRunning)
            {
                _logger.Info("Engine already running");
                return;
            }

            _engine = new DaqEngine(_logger);
            _engine.OutputIntervalMicroseconds = outputIntervalUs;

            // CRIT-01: Always use shared HALs from DeviceManager's SDK instances.
            // Never let DaqEngine create its own SDK instances (avoids dual-handle problem).
            if (digitalDeviceNumber >= 0)
            {
                // If DeviceManager has digital devices initialized, share them
                if (_deviceManager.IsInitialized && _deviceManager.CurrentDeviceType == DeviceType.Digital 
                    && _concreteDeviceManager?.DigitalInputDevice != null)
                {
                    _engine.InitializeDigitalFromExisting(
                        _concreteDeviceManager.DigitalInputDevice, 
                        _concreteDeviceManager.DigitalOutputDevice);
                }
                else
                {
                    // Fallback: no DeviceManager digital devices, let engine create its own
                    _engine.InitializeDigital(digitalDeviceNumber);
                }
            }

            if (analogDeviceNumber >= 0)
            {
                // If DeviceManager has analog device initialized, share it
                if (_deviceManager.IsInitialized && _deviceManager.CurrentDeviceType == DeviceType.Analog 
                    && _concreteDeviceManager?.Device != null)
                {
                    _engine.InitializeAnalogFromExisting(_concreteDeviceManager.Device);
                }
                else
                {
                    // Fallback: no DeviceManager analog device, let engine create its own
                    _engine.InitializeAnalog(analogDeviceNumber);
                }
            }
            else if (_deviceManager.IsInitialized && _deviceManager.CurrentDeviceType == DeviceType.Analog 
                     && _concreteDeviceManager?.Device != null)
            {
                _engine.InitializeAnalogFromExisting(_concreteDeviceManager.Device);
            }

            _engine.Start();
            _logger.Info($"DaqEngine started (Digital={_engine.HasDigital}, Analog={_engine.HasAnalog})");
        }

        /// <summary>
        /// Stops and disposes the high-performance engine.
        /// Operations fall back to direct DeviceManager calls.
        /// </summary>
        public void StopEngine()
        {
            if (_engine != null)
            {
                _engine.Stop();
                _engine.Dispose();
                _engine = null;
                _logger.Info("DaqEngine stopped");
            }
        }

        /// <summary>
        /// Gets engine diagnostics string.
        /// </summary>
        public string GetEngineDiagnostics()
        {
            return _engine?.GetDiagnostics() ?? "Engine not running";
        }
        #endregion

        #region Private Methods

        private void ValidateChannelNumber(int channel)
        {
            if (channel < 0 || (ChannelCount > 0 && channel >= ChannelCount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(channel), 
                    $"Channel must be between 0 and {ChannelCount - 1}");
            }
        }
        
        private void ValidatePortNumber(int port)
        {
            if (port < 0 || port > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(port),
                    "Puerto debe estar entre 0 y 3");
            }
        }
        
        private void ValidateBitNumber(int bit)
        {
            if (bit < 0 || bit > 7)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bit),
                    "Bit debe estar entre 0 y 7");
            }
        }
        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DAQController));
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Stop engine first if running
                    StopEngine();
                    
                    // Stop any signal generation first
                    StopSignalGeneration();
                    
                    // Reset all channels
                    _channelManager?.ResetAllChannels();
                    
                    // Dispose shared HALs (they don't own SDK instances, just release wrappers)
                    _sharedAnalogHal?.Dispose();
                    _sharedDigitalHal?.Dispose();
                    
                    // Dispose managers and services
                    (_deviceManager as IDisposable)?.Dispose();
                    (_signalGenerator as IDisposable)?.Dispose();
                    
                    _logger.Info("DAQ controller resources released");
                }
                catch (Exception ex)
                {
                    _logger.Error("Error releasing resources", ex);
                }
                
                _disposed = true;
            }
        }

        ~DAQController()
        {
            Dispose(false);
        }
        #endregion
    }
}

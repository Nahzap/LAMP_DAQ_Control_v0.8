using System;
using System.Diagnostics;
using System.Threading;
using LAMP_DAQ_Control_v0_8.Core.DAQ.HAL;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Engine
{
    /// <summary>
    /// Central orchestrator for the high-performance DAQ engine.
    /// Wires together all engine components:
    ///   HAL (hardware) → InputPoller (read) → StateGrid (state) → 
    ///   LogicPipeline (compute) → OutputMetronome (timing) → Dispatcher (write)
    /// 
    /// Usage:
    ///   var engine = new DaqEngine(logger);
    ///   engine.InitializeDigital(boardId);   // optional
    ///   engine.InitializeAnalog(boardId);    // optional
    ///   engine.Start();
    ///   
    ///   // Command-driven writes (from DAQController/UI):
    ///   engine.WriteDigitalBit(port, bit, value);
    ///   engine.WriteAnalogVoltage(channel, voltage);
    ///   
    ///   engine.Stop();
    ///   engine.Dispose();
    /// </summary>
    public class DaqEngine : IDisposable
    {
        private readonly ILogger _logger;
        private bool _disposed;

        // HAL layer
        private AdvantechDigitalHal _digitalHal;
        private AdvantechAnalogHal _analogHal;

        // Engine components
        private StateGrid _stateGrid;
        private HighSpeedInputPoller _inputPoller;
        private LogicPipeline _logicPipeline;
        private SynchronizedOutputDispatcher _dispatcher;
        private OutputMetronome _metronome;

        // Configuration
        private bool _digitalInitialized;
        private bool _analogInitialized;
        private bool _engineRunning;
        private int _outputIntervalMicroseconds = 500; // Default 2kHz

        /// <summary>
        /// Whether the engine is currently running.
        /// </summary>
        public bool IsRunning => _engineRunning;

        /// <summary>
        /// Whether digital hardware is initialized.
        /// </summary>
        public bool HasDigital => _digitalInitialized && _digitalHal != null && _digitalHal.IsReady;

        /// <summary>
        /// Whether analog hardware is initialized.
        /// </summary>
        public bool HasAnalog => _analogInitialized && _analogHal != null && _analogHal.IsReady;

        /// <summary>
        /// The underlying StateGrid for external state queries.
        /// </summary>
        public StateGrid StateGrid => _stateGrid;

        /// <summary>
        /// The underlying AnalogHal for backward compatibility (SignalGenerator needs InstantAoCtrl).
        /// </summary>
        public AdvantechAnalogHal AnalogHal => _analogHal;

        /// <summary>
        /// The underlying DigitalHal for direct access if needed.
        /// </summary>
        public AdvantechDigitalHal DigitalHal => _digitalHal;

        /// <summary>
        /// The LogicPipeline for registering custom process callbacks.
        /// </summary>
        public LogicPipeline Pipeline => _logicPipeline;

        /// <summary>
        /// Output cycle interval in microseconds.
        /// </summary>
        public int OutputIntervalMicroseconds
        {
            get { return _outputIntervalMicroseconds; }
            set
            {
                _outputIntervalMicroseconds = value;
                if (_metronome != null)
                    _metronome.IntervalMicroseconds = value;
            }
        }

        public DaqEngine(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.Info("[DaqEngine] Created");
        }

        #region Initialization

        /// <summary>
        /// Initializes the digital HAL (PCI-1735U) with the specified Board ID.
        /// Can be called before or after InitializeAnalog.
        /// </summary>
        public bool InitializeDigital(int deviceNumber)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DaqEngine));

            try
            {
                if (_digitalHal != null)
                {
                    _digitalHal.Dispose();
                    _digitalHal = null;
                }

                _digitalHal = new AdvantechDigitalHal(_logger);
                _digitalInitialized = _digitalHal.Initialize(deviceNumber);

                if (_digitalInitialized)
                    _logger.Info($"[DaqEngine] Digital HAL initialized (Board ID: {deviceNumber})");
                else
                    _logger.Warn($"[DaqEngine] Digital HAL failed to initialize (Board ID: {deviceNumber})");

                return _digitalInitialized;
            }
            catch (Exception ex)
            {
                _logger.Error("[DaqEngine] Digital initialization error", ex);
                _digitalInitialized = false;
                return false;
            }
        }

        /// <summary>
        /// Initializes the analog HAL (PCIe-1824) with the specified Board ID.
        /// Can be called before or after InitializeDigital.
        /// </summary>
        public bool InitializeAnalog(int deviceNumber)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DaqEngine));

            try
            {
                if (_analogHal != null)
                {
                    _analogHal.Dispose();
                    _analogHal = null;
                }

                _analogHal = new AdvantechAnalogHal(_logger);
                _analogInitialized = _analogHal.Initialize(deviceNumber);

                if (_analogInitialized)
                    _logger.Info($"[DaqEngine] Analog HAL initialized (Board ID: {deviceNumber}), {_analogHal.ChannelCount} channels");
                else
                    _logger.Warn($"[DaqEngine] Analog HAL failed to initialize (Board ID: {deviceNumber})");

                return _analogInitialized;
            }
            catch (Exception ex)
            {
                _logger.Error("[DaqEngine] Analog initialization error", ex);
                _analogInitialized = false;
                return false;
            }
        }

        /// <summary>
        /// Initializes the digital HAL from existing SDK controller instances.
        /// Used to share SDK handles with DeviceManager (CRIT-01 fix).
        /// </summary>
        public void InitializeDigitalFromExisting(Automation.BDaq.InstantDiCtrl existingDi, Automation.BDaq.InstantDoCtrl existingDo)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DaqEngine));

            if (_digitalHal != null)
            {
                _digitalHal.Dispose();
                _digitalHal = null;
            }

            _digitalHal = new AdvantechDigitalHal(_logger);
            _digitalHal.InitializeFromExisting(existingDi, existingDo);
            _digitalInitialized = _digitalHal.IsReady;

            _logger.Info($"[DaqEngine] Digital HAL initialized from existing devices, Ready={_digitalInitialized}");
        }

        /// <summary>
        /// Initializes the analog HAL from an existing InstantAoCtrl instance.
        /// Used for backward compatibility with existing DeviceManager.
        /// </summary>
        public void InitializeAnalogFromExisting(Automation.BDaq.InstantAoCtrl existingDevice)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DaqEngine));

            if (_analogHal != null)
            {
                _analogHal.Dispose();
                _analogHal = null;
            }

            _analogHal = new AdvantechAnalogHal(_logger);
            _analogHal.InitializeFromExisting(existingDevice);
            _analogInitialized = _analogHal.IsReady;

            _logger.Info($"[DaqEngine] Analog HAL initialized from existing device, Ready={_analogInitialized}");
        }

        #endregion

        #region Engine Lifecycle

        /// <summary>
        /// Starts all engine components.
        /// Components are started bottom-up: StateGrid → InputPoller → Pipeline → Dispatcher → Metronome.
        /// Only components with available hardware are started.
        /// </summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DaqEngine));
            if (_engineRunning)
            {
                _logger.Warn("[DaqEngine] Already running");
                return;
            }

            if (!HasDigital && !HasAnalog)
            {
                _logger.Warn("[DaqEngine] No hardware initialized, cannot start");
                return;
            }

            _logger.Info("[DaqEngine] Starting engine...");

            // 1. StateGrid
            int analogChannels = _analogHal?.ChannelCount ?? 32;
            _stateGrid = new StateGrid(analogChannels, _logger);

            // 2. InputPoller (only if digital HAL is ready)
            if (HasDigital)
            {
                _inputPoller = new HighSpeedInputPoller(_digitalHal, _stateGrid, _logger);
                _inputPoller.Start();
            }

            // 3. LogicPipeline
            _logicPipeline = new LogicPipeline(_stateGrid, _inputPoller, _logger);
            _logicPipeline.Start();

            // 4. SynchronizedOutputDispatcher
            _dispatcher = new SynchronizedOutputDispatcher(
                HasDigital ? _digitalHal : null,
                HasAnalog ? _analogHal : null,
                _stateGrid,
                _logger);
            _dispatcher.Start();

            // 5. OutputMetronome
            _metronome = new OutputMetronome(_dispatcher, _stateGrid, _logger);
            _metronome.IntervalMicroseconds = _outputIntervalMicroseconds;
            _metronome.Start();

            _engineRunning = true;
            _logger.Info($"[DaqEngine] Engine started (Digital={HasDigital}, Analog={HasAnalog}, Rate={1_000_000.0 / _outputIntervalMicroseconds:F0}Hz)");
        }

        /// <summary>
        /// Stops all engine components in reverse order.
        /// </summary>
        public void Stop()
        {
            if (!_engineRunning)
                return;

            _logger.Info("[DaqEngine] Stopping engine...");

            _metronome?.Stop();
            _dispatcher?.Stop();
            _logicPipeline?.Stop();
            _inputPoller?.Stop();

            _engineRunning = false;
            _logger.Info("[DaqEngine] Engine stopped");
        }

        #endregion

        #region Command API (used by DAQController, ExecutionEngine, ViewModels)

        /// <summary>
        /// Writes a single digital bit through the engine pipeline.
        /// The write is queued and executed at the next metronome cycle.
        /// </summary>
        public void WriteDigitalBit(int port, int bit, bool value)
        {
            if (!HasDigital)
            {
                _logger.Warn("[DaqEngine] WriteDigitalBit: No digital hardware");
                return;
            }

            if (_engineRunning && _logicPipeline != null)
            {
                _logicPipeline.RequestDigitalWrite(port, bit, value);
            }
            else
            {
                // Direct write (engine not running)
                _digitalHal.WriteOutputsMasked(
                    value ? (1u << (port * 8 + bit)) : 0u,
                    (byte)(1 << port));
            }
        }

        /// <summary>
        /// Writes a full digital port through the engine pipeline.
        /// </summary>
        public void WriteDigitalPort(int port, byte value)
        {
            if (!HasDigital)
            {
                _logger.Warn("[DaqEngine] WriteDigitalPort: No digital hardware");
                return;
            }

            if (_engineRunning && _logicPipeline != null)
            {
                _logicPipeline.RequestDigitalPortWrite(port, value);
            }
            else
            {
                _digitalHal.WriteOutputsMasked((uint)value << (port * 8), (byte)(1 << port));
            }
        }

        /// <summary>
        /// Reads the current digital input state (32 bits = 4 ports).
        /// Uses the StateGrid cached value if engine is running, otherwise reads directly.
        /// </summary>
        public uint ReadDigitalInputs()
        {
            if (!HasDigital)
                return 0;

            if (_engineRunning && _stateGrid != null)
                return _stateGrid.ActiveInputMask;

            return _digitalHal.ReadInputs();
        }

        /// <summary>
        /// Reads a single digital input bit.
        /// </summary>
        public bool ReadDigitalBit(int port, int bit)
        {
            uint state = ReadDigitalInputs();
            uint mask = 1u << (port * 8 + bit);
            return (state & mask) != 0;
        }

        /// <summary>
        /// Reads a full digital input port (8 bits).
        /// </summary>
        public byte ReadDigitalPort(int port)
        {
            uint state = ReadDigitalInputs();
            return (byte)((state >> (port * 8)) & 0xFF);
        }

        /// <summary>
        /// Writes a single analog voltage through the engine pipeline.
        /// The write is queued and executed at the next metronome cycle.
        /// </summary>
        public void WriteAnalogVoltage(int channel, double voltage)
        {
            if (!HasAnalog)
            {
                _logger.Warn("[DaqEngine] WriteAnalogVoltage: No analog hardware");
                return;
            }

            if (_engineRunning && _logicPipeline != null)
            {
                _logicPipeline.RequestAnalogWrite(channel, voltage);
            }
            else
            {
                // Direct write (engine not running)
                _analogHal.WriteSingle(channel, voltage);
            }
        }

        /// <summary>
        /// Writes multiple analog voltages through the engine pipeline using a bitmask.
        /// </summary>
        public void WriteAnalogBatch(double[] voltages, uint activeMask)
        {
            if (!HasAnalog)
                return;

            if (_engineRunning && _logicPipeline != null)
            {
                _logicPipeline.RequestAnalogWriteBatch(voltages, activeMask);
            }
            else
            {
                _analogHal.WriteOutputs(voltages, activeMask);
            }
        }

        /// <summary>
        /// Resets all outputs to zero (both digital and analog).
        /// </summary>
        public void ResetAllOutputs()
        {
            _logger.Info("[DaqEngine] Resetting all outputs to zero...");

            if (_stateGrid != null)
            {
                _stateGrid.Reset();
            }

            if (HasDigital)
            {
                try { _digitalHal.WriteOutputs(0); } catch { }
            }

            if (HasAnalog)
            {
                try
                {
                    for (int ch = 0; ch < _analogHal.ChannelCount; ch++)
                    {
                        _analogHal.WriteSingle(ch, 0.0);
                    }
                }
                catch { }
            }

            _logger.Info("[DaqEngine] All outputs reset to zero");
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Gets a diagnostic summary string.
        /// </summary>
        public string GetDiagnostics()
        {
            return $"DaqEngine: Running={_engineRunning}, Digital={HasDigital}, Analog={HasAnalog}\n" +
                   $"  InputPoller: {(_inputPoller?.TotalReads ?? 0)} reads, {(_inputPoller?.TotalChanges ?? 0)} changes\n" +
                   $"  Pipeline: {(_logicPipeline?.ProcessedEvents ?? 0)} events, {(_logicPipeline?.ProcessedCycles ?? 0)} cycles\n" +
                   $"  Dispatcher: {(_dispatcher?.DigitalWriteCount ?? 0)} digital, {(_dispatcher?.AnalogWriteCount ?? 0)} analog\n" +
                   $"  Metronome: {(_metronome?.TotalCycles ?? 0)} cycles, {(_metronome?.SkippedCycles ?? 0)} idle";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                Stop();
                ResetAllOutputs();

                _metronome?.Dispose();
                _dispatcher?.Dispose();
                _logicPipeline?.Dispose();
                _inputPoller?.Dispose();

                _digitalHal?.Dispose();
                _analogHal?.Dispose();

                _logger.Info("[DaqEngine] Disposed");
            }
        }

        #endregion
    }
}

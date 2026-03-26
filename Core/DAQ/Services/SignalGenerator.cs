using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Exceptions;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    /// <summary>
    /// Handles signal generation for DAQ devices
    /// </summary>
    public class SignalGenerator : ISignalGenerator
    {
        private readonly InstantAoCtrl _device;
        private readonly ILogger _logger;
        private bool _disposed;
        private int _currentChannel = -1;

        // Diccionario para manejar múltiples canales activos simultáneamente
        private readonly Dictionary<int, CancellationTokenSource> _activeChannels = new Dictionary<int, CancellationTokenSource>();
        private readonly object _activeChannelsLock = new object();
        
        // CRITICAL FIX: Track last written values per channel for smooth ramps
        private readonly Dictionary<int, double> _lastWrittenValues = new Dictionary<int, double>();
        private readonly object _lastWrittenValuesLock = new object();
        
        // HIGH-PRECISION TIMING: Stopwatch calibration for nanosecond accuracy
        private static readonly double _ticksToNanoseconds;
        
        // CRITICAL: Pre-parsed LUT values cached as double[] for zero-allocation hot loop
        private static double[] _cachedNormalizedValues = null;
        private static int _cachedLutSize = 0;
        private static readonly object _lutCacheLock = new object();
        
        // PHASE SYNC: Barrier for synchronized waveform start across multiple channels
        private static Barrier _phaseBarrier = null;
        private static readonly object _barrierLock = new object();
        
        static SignalGenerator()
        {
            // Calibrate Stopwatch ticks to nanoseconds conversion
            _ticksToNanoseconds = 1_000_000_000.0 / Stopwatch.Frequency;
        }

        /// <summary>
        /// Initializes a new instance of the SignalGenerator class
        /// </summary>
        /// <param name="device">The DAQ device to use</param>
        /// <param name="logger">Logger instance</param>
        public SignalGenerator(InstantAoCtrl device, ILogger logger = null)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _logger = logger ?? new ConsoleLogger();
        }

        /// <summary>
        /// Starts signal generation on the specified channel
        /// </summary>
        public void Start(int channel, double frequency, double amplitude, double offset)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SignalGenerator));
            if (channel < 0 || _device.Channels == null || channel >= _device.Channels.Length)
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be greater than zero");
            if (amplitude <= 0) throw new ArgumentOutOfRangeException(nameof(amplitude), "Amplitude must be greater than zero");

            try
            {
                CancellationTokenSource cts;
                
                // CRITICAL: Lock _activeChannels to prevent race condition between parallel waveforms
                lock (_activeChannelsLock)
                {
                    // Stop any existing signal generation on this channel
                    if (_activeChannels.ContainsKey(channel))
                    {
                        // Cancelar la generación existente para este canal
                        var existingCts = _activeChannels[channel];
                        if (existingCts != null && !existingCts.IsCancellationRequested)
                        {
                            existingCts.Cancel();
                            existingCts.Dispose();
                        }
                        _activeChannels.Remove(channel);
                    }

                    // Create a new cancellation token source for this channel
                    cts = new CancellationTokenSource();
                    _activeChannels[channel] = cts;
                }

                // Start the signal generation on a background thread with high priority
                var thread = new Thread(() => GenerateSignal(channel, frequency, amplitude, offset, cts.Token));
                thread.Priority = ThreadPriority.Highest; // Set highest priority for better timing precision
                thread.IsBackground = true;
                thread.Start();
                
                _logger.Info($"Signal generation started on channel {channel}: {frequency}Hz, {amplitude}V, {offset}V offset");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error starting signal generation on channel {channel}", ex);
                throw new DAQOperationException($"Failed to start signal generation on channel {channel}", ex);
            }
        }

        /// <summary>
        /// Pre-loads the LUT cache to avoid cache miss delays during parallel waveform execution
        /// CRITICAL: Call this BEFORE starting parallel waveforms to ensure phase synchronization
        /// </summary>
        public static void PreloadLutCache()
        {
            lock (_lutCacheLock)
            {
                if (_cachedNormalizedValues != null)
                {
                    // Already loaded
                    return;
                }
                
                string csvPath = SignalLUT.GetFullLutPath(SignalLUTs.SIN_LUT_FILENAME);
                
                if (!File.Exists(csvPath))
                {
                    // Generate if missing
                    SignalLUT.GenerateSineLutFile(SignalLUTs.SIN_LUT_FILENAME, SignalLUTs.RECOMMENDED_LUT_SIZE);
                }
                
                var lutLines = File.ReadAllLines(csvPath);
                int size = lutLines.Length - 1; // Skip header
                _cachedNormalizedValues = new double[size];
                
                for (int idx = 0; idx < size; idx++)
                {
                    string line = lutLines[idx + 1];
                    if (!string.IsNullOrEmpty(line))
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length >= 2 && ushort.TryParse(parts[1], out ushort val))
                        {
                            _cachedNormalizedValues[idx] = val / 65535.0;
                            continue;
                        }
                    }
                    _cachedNormalizedValues[idx] = 0.5;
                }
                
                _cachedLutSize = size;
            }
        }
        
        /// <summary>
        /// Prepares a phase synchronization barrier for N parallel waveforms
        /// CRITICAL: Call this before launching parallel waveforms, then call Start() on each channel
        /// </summary>
        public static void PreparePhaseBarrier(int participantCount)
        {
            lock (_barrierLock)
            {
                _phaseBarrier?.Dispose();
                _phaseBarrier = new Barrier(participantCount);
            }
        }
        
        /// <summary>
        /// Clears the phase barrier after parallel waveforms have started
        /// </summary>
        public static void ClearPhaseBarrier()
        {
            lock (_barrierLock)
            {
                _phaseBarrier?.Dispose();
                _phaseBarrier = null;
            }
        }
        
        /// <summary>
        /// Stops signal generation on all channels
        /// </summary>
        public void Stop()
        {
            List<int> channels;
            
            // CRITICAL: Lock to get snapshot of active channels
            lock (_activeChannelsLock)
            {
                channels = _activeChannels.Keys.ToList();
            }
            
            // Stop all channels (StopChannel has its own lock)
            foreach (var channel in channels)
            {
                StopChannel(channel);
            }
        }
        
        /// <summary>
        /// Stops signal generation on a specific channel
        /// </summary>
        public void StopChannel(int channel)
        {
            CancellationTokenSource cts = null;
            
            // CRITICAL: Lock to safely remove from dictionary
            lock (_activeChannelsLock)
            {
                if (_activeChannels.ContainsKey(channel))
                {
                    cts = _activeChannels[channel];
                    _activeChannels.Remove(channel);
                }
            }
            
            // Cancel and dispose outside lock to avoid holding lock during I/O
            if (cts != null)
            {
                try
                {
                    if (!cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    
                    // Reset the channel to 0V
                    _device.Write(channel, 0.0);
                    
                    _logger.Debug($"Signal generation stopped on channel {channel}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error stopping signal generation on channel {channel}", ex);
                }
            }
        }

        /// <summary>
        /// Sets a DC value on the specified channel
        /// </summary>
        public void SetDcValue(int channel, double value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SignalGenerator));
            if (channel < 0 || _device.Channels == null || channel >= _device.Channels.Length)
                throw new ArgumentOutOfRangeException(nameof(channel));

            try
            {
                _device.Write(channel, value);
                
                // Track last written value (thread-safe)
                lock (_lastWrittenValuesLock)
                {
                    _lastWrittenValues[channel] = value;
                }
                
                _logger.Debug($"DC value set on channel {channel}: {value}V");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error setting DC value on channel {channel}", ex);
                throw new DAQOperationException($"Failed to set DC value on channel {channel}", ex);
            }
        }

        /// <summary>
        /// Ramps a channel to a target value over the specified duration with HIGH PRECISION timing
        /// </summary>
        public async Task SetDcValueAsync(int channel, double targetValue, int durationMs)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SignalGenerator));
            if (durationMs <= 0) throw new ArgumentOutOfRangeException(nameof(durationMs));
            if (channel < 0 || _device.Channels == null || channel >= _device.Channels.Length)
                throw new ArgumentOutOfRangeException(nameof(channel));

            try
            {
                const int steps = 100;
                
                // CRITICAL FIX: Get last written value for this channel (thread-safe)
                double currentValue;
                lock (_lastWrittenValuesLock)
                {
                    currentValue = _lastWrittenValues.ContainsKey(channel) 
                        ? _lastWrittenValues[channel] 
                        : 0.0;
                }
                
                // COMPRESSED LOGGING: Solo inicio y fin
                System.Console.WriteLine($"[RAMP START] CH{channel}: {currentValue:F3}V → {targetValue:F3}V ({durationMs}ms, {steps} steps)");
                
                double voltageStep = (targetValue - currentValue) / steps;
                long durationNs = durationMs * 1_000_000L;
                long stepNs = durationNs / steps;
                
                var rampTimer = Stopwatch.StartNew();
                long stepDelayTicks = (long)(stepNs / _ticksToNanoseconds);
                
                for (int i = 0; i < steps; i++)
                {
                    if (_disposed) return;
                    
                    long targetTicks = (i + 1) * stepDelayTicks;
                    currentValue += voltageStep;
                    _device.Write(channel, currentValue);
                    await HighPrecisionWaitAsync(rampTimer, targetTicks);
                }

                _device.Write(channel, targetValue);
                
                // Thread-safe update of last written value
                lock (_lastWrittenValuesLock)
                {
                    _lastWrittenValues[channel] = targetValue;
                }
                
                rampTimer.Stop();
                long totalNs = (long)(rampTimer.ElapsedTicks * _ticksToNanoseconds);
                long errorNs = totalNs - durationNs;
                double errorPercent = (errorNs / (double)durationNs) * 100.0;
                
                System.Console.WriteLine($"[RAMP END] CH{channel}: {targetValue:F3}V | {totalNs / 1_000_000.0:F1}ms (err: {errorPercent:F2}%)");
                
                _logger.Debug($"Ramp completed on channel {channel}: {currentValue:F3}V → {targetValue:F3}V in {totalNs / 1_000_000.0:F3}ms");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[RAMP ERROR] Channel {channel}: {ex.Message}");
                _logger.Error($"Error during ramp operation on channel {channel}", ex);
                throw new DAQOperationException($"Ramp operation failed on channel {channel}", ex);
            }
        }
        
        /// <summary>
        /// High-precision async wait using hybrid approach (SpinWait for precision, Task.Delay for efficiency)
        /// </summary>
        private async Task HighPrecisionWaitAsync(Stopwatch timer, long targetTicks)
        {
            const long SPIN_THRESHOLD_TICKS = 200_000; // ~20ms threshold for spinning to prevent Windows oversleep
            const long TASK_DELAY_MARGIN_TICKS = 50_000; // ~5ms margin for Task.Delay imprecision
            
            long remainingTicks = targetTicks - timer.ElapsedTicks;
            
            // PHASE 1: Coarse wait with Task.Delay (if remaining time > threshold)
            if (remainingTicks > SPIN_THRESHOLD_TICKS)
            {
                long coarseWaitTicks = remainingTicks - TASK_DELAY_MARGIN_TICKS;
                if (coarseWaitTicks > 0)
                {
                    long coarseWaitNs = (long)(coarseWaitTicks * _ticksToNanoseconds);
                    int coarseWaitMs = (int)(coarseWaitNs / 1_000_000);
                    if (coarseWaitMs > 0)
                    {
                        await Task.Delay(coarseWaitMs);
                    }
                }
            }
            
            // PHASE 2: Precision wait with SpinWait (final microseconds)
            SpinWait spinner = new SpinWait();
            while (timer.ElapsedTicks < targetTicks)
            {
                spinner.SpinOnce();
            }
        }

        /// <summary>
        /// Resets all outputs to their default state (0V)
        /// </summary>
        public void ResetAllOutputs()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SignalGenerator));

            try
            {
                Stop();
                if (_device.Channels != null)
                {
                    for (int i = 0; i < _device.Channels.Length; i++)
                    {
                        _device.Write(i, 0.0);
                    }
                }
                _logger.Info("All outputs reset to 0V");
            }
            catch (Exception ex)
            {
                _logger.Error("Error resetting outputs", ex);
                throw new DAQOperationException("Failed to reset outputs", ex);
            }
        }

        /// <summary>
        /// Checks if the specified channel is currently active (outputting a signal)
        /// </summary>
        /// <param name="channel">Channel number to check</param>
        /// <returns>True if the channel is active, false otherwise</returns>
        public bool IsChannelActive(int channel)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SignalGenerator));
            if (channel < 0 || _device.Channels == null || channel >= _device.Channels.Length)
                throw new ArgumentOutOfRangeException(nameof(channel));

            // Un canal se considera activo si está en el diccionario de canales activos
            return _activeChannels.ContainsKey(channel);
        }

        #region IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Stop();
                        _logger.Info("Signal generator disposed");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error during signal generator disposal", ex);
                    }
                    finally
                    {
                        _disposed = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// Generates a continuous sine wave signal on the specified channel with ultra-low jitter
        /// </summary>
        private void GenerateSignal(int channel, double frequency, double amplitude, double offset, CancellationToken cancellationToken)
        {
            try
            {
                // Establecer prioridad de thread alta para mejorar la temporización
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                
                // Obtener la ruta al archivo CSV de la LUT usando el método de SignalLUT
                string csvPath = SignalLUT.GetFullLutPath(SignalLUTs.SIN_LUT_FILENAME);
                
                // Verificar que el archivo CSV existe
                if (!File.Exists(csvPath))
                {
                    _logger.Info($"Archivo LUT CSV no encontrado: {csvPath}. Generando automáticamente...");
                    try
                    {
                        // Generar el archivo CSV si no existe
                        SignalLUT.GenerateSineLutFile(SignalLUTs.SIN_LUT_FILENAME, SignalLUTs.RECOMMENDED_LUT_SIZE);
                        
                        // Verificar que se haya creado correctamente
                        if (!File.Exists(csvPath))
                        {
                            _logger.Error($"Error: No se pudo generar el archivo LUT CSV: {csvPath}");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error al generar el archivo LUT CSV: {ex.Message}");
                        return;
                    }
                }
                
                // CRITICAL PERFORMANCE: Pre-parse LUT to double[] (load once, zero allocations in hot loop)
                int lutSize;
                
                lock (_lutCacheLock)
                {
                    if (_cachedNormalizedValues == null)
                    {
                        _logger.Info($"[CACHE MISS] Loading and pre-parsing LUT CSV: {csvPath}");
                        var lutLines = File.ReadAllLines(csvPath);
                        int size = lutLines.Length - 1; // Skip header
                        _cachedNormalizedValues = new double[size];
                        
                        for (int idx = 0; idx < size; idx++)
                        {
                            string line = lutLines[idx + 1];
                            if (!string.IsNullOrEmpty(line))
                            {
                                string[] parts = line.Split(',');
                                if (parts.Length >= 2 && ushort.TryParse(parts[1], out ushort val))
                                {
                                    _cachedNormalizedValues[idx] = val / 65535.0;
                                    continue;
                                }
                            }
                            _cachedNormalizedValues[idx] = 0.5;
                        }
                        
                        _cachedLutSize = size;
                        _logger.Info($"[CACHE LOADED] {size} values pre-parsed to double[]");
                    }
                    else
                    {
                        _logger.Info($"[CACHE HIT] Using pre-parsed LUT (no disk I/O, no string parsing)");
                    }
                    
                    lutSize = _cachedLutSize;
                }
                
                // OPTIMIZACIÓN: Escribir valor inicial directamente sin delays
                _device.Write(channel, offset);
                
                // ===== PRE-COMPUTE EVERYTHING BEFORE BARRIER =====
                const double TARGET_SAMPLE_RATE = 100000.0; // 100 kHz
                int samplesPerCycle = (int)(TARGET_SAMPLE_RATE / frequency);
                if (samplesPerCycle < 20) {
                    samplesPerCycle = 20;
                } else if (samplesPerCycle > 10000) {
                    samplesPerCycle = 10000;
                }
                double sampleRate = frequency * samplesPerCycle;
                long ticksPerSample = (long)(System.Diagnostics.Stopwatch.Frequency / sampleRate);
                
                _logger.Info($"Starting sine wave generation on channel {channel}: {frequency}Hz, {amplitude}V amplitude, {offset}V offset");
                _logger.Info($"Using {samplesPerCycle} samples per cycle at {sampleRate:F0} samples/sec for {frequency}Hz signal");
                
                // Start stopwatch BEFORE barrier so ElapsedTicks is available immediately after release
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
                
                // ===== PHASE SYNC BARRIER =====
                Barrier barrier = null;
                lock (_barrierLock)
                {
                    barrier = _phaseBarrier;
                }
                
                if (barrier != null)
                {
                    _logger.Info($"[PHASE SYNC] CH{channel} ready - waiting at barrier...");
                    try
                    {
                        barrier.SignalAndWait(2000); // 2s timeout
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"[PHASE SYNC] CH{channel} barrier error: {ex.Message}");
                    }
                }
                
                // ===== CRITICAL: FIRST INSTRUCTION AFTER BARRIER RELEASE =====
                // Capture epoch IMMEDIATELY - ZERO code between barrier release and this line
                long startTicks = stopwatch.ElapsedTicks;
                
                // These can be set after epoch capture (not time-critical)
                long totalSampleCount = 0;
                long lastStatsReportTicks = startTicks;
                
                // Generar la señal continuamente hasta que se cancele
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Generar exactamente un ciclo completo
                    for (int i = 0; i < samplesPerCycle; i++)
                    {
                        // Calcular la fase actual (0.0 a 1.0)
                        double phase = (double)i / samplesPerCycle;
                        
                        // Calcular el índice en la LUT basado en la fase
                        int lutIndex = (int)(phase * lutSize);
                        lutIndex = Math.Max(0, Math.Min(lutIndex, lutSize - 1)); // Asegurar que esté en rango
                        
                        // Zero-allocation: direct array access to pre-parsed normalized values
                        double normalizedValue = _cachedNormalizedValues[lutIndex];
                        
                        // Convertir a voltaje de salida (aplicando amplitud y offset)
                        double sineValue = (normalizedValue * 2.0) - 1.0; // Convertir [0,1] a [-1,1]
                        double outputVoltage = Math.Max(0.0, Math.Min(10.0, (sineValue * amplitude) + offset));
                        
                        // Escribir al DAC
                        _device.Write(channel, outputVoltage);
                        
                        // ABSOLUTE TIMING: Target time computed from epoch, not per-cycle start
                        totalSampleCount++;
                        long targetTicks = startTicks + totalSampleCount * ticksPerSample;
                        
                        // Primera fase: espera gruesa con mínimo uso de CPU
                        while (stopwatch.ElapsedTicks < targetTicks - 20)
                        {
                            Thread.SpinWait(1);
                        }
                        
                        // Segunda fase: espera activa pura para temporización exacta
                        while (stopwatch.ElapsedTicks < targetTicks)
                        {
                            // Bucle vacío para máxima precisión
                        }
                    }
                    
                    // Registrar estadísticas cada segundo (NO restart - absolute timing preserved)
                    long currentTicks = stopwatch.ElapsedTicks;
                    long ticksSinceLastReport = currentTicks - lastStatsReportTicks;
                    if (ticksSinceLastReport > System.Diagnostics.Stopwatch.Frequency) // > 1 second
                    {
                        double elapsedSeconds = (double)ticksSinceLastReport / System.Diagnostics.Stopwatch.Frequency;
                        double totalElapsed = (double)(currentTicks - startTicks) / System.Diagnostics.Stopwatch.Frequency;
                        double expectedCycles = totalElapsed * frequency;
                        double actualCycles = (double)totalSampleCount / samplesPerCycle;
                        _logger.Debug($"Signal CH{channel}: {actualCycles / totalElapsed:F2}Hz actual, drift: {(actualCycles - expectedCycles):F4} cycles after {totalElapsed:F1}s");
                        lastStatsReportTicks = currentTicks;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, no need to log
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in signal generation thread for channel {channel}", ex);
            }
            finally
            {
                // Restablecer prioridad de thread al salir
                try { Thread.CurrentThread.Priority = ThreadPriority.Normal; } catch { }
            }
        }
        #endregion
    }
}

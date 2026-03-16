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
        
        // CRITICAL FIX: Track last written values per channel for smooth ramps
        private readonly Dictionary<int, double> _lastWrittenValues = new Dictionary<int, double>();
        
        // HIGH-PRECISION TIMING: Stopwatch calibration for nanosecond accuracy
        private static readonly double _ticksToNanoseconds;
        
        static SignalGenerator()
        {
            // Calibrate Stopwatch ticks to nanoseconds conversion
            _ticksToNanoseconds = 1_000_000_000.0 / Stopwatch.Frequency;
            System.Console.WriteLine($"[SIGNAL GEN CALIBRATION] Stopwatch Frequency: {Stopwatch.Frequency} Hz");
            System.Console.WriteLine($"[SIGNAL GEN CALIBRATION] Ticks to Nanoseconds: {_ticksToNanoseconds:F6} ns/tick");
            System.Console.WriteLine($"[SIGNAL GEN CALIBRATION] High Resolution: {Stopwatch.IsHighResolution}");
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
                var cts = new CancellationTokenSource();
                _activeChannels[channel] = cts;

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
        /// Stops signal generation on all channels
        /// </summary>
        public void Stop()
        {
            // Detener la generación en todos los canales activos
            foreach (var channel in _activeChannels.Keys.ToList())
            {
                StopChannel(channel);
            }
            
            // Limpiar el diccionario de canales activos
            _activeChannels.Clear();
        }
        
        /// <summary>
        /// Stops signal generation on a specific channel
        /// </summary>
        public void StopChannel(int channel)
        {
            if (_activeChannels.ContainsKey(channel))
            {
                try
                {
                    // Cancelar la generación para este canal
                    var cts = _activeChannels[channel];
                    if (cts != null && !cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    
                    // Eliminar el canal del diccionario
                    _activeChannels.Remove(channel);
                    
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
                // Track last written value
                _lastWrittenValues[channel] = value;
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
                
                // CRITICAL FIX: Get last written value for this channel
                double currentValue = _lastWrittenValues.ContainsKey(channel) 
                    ? _lastWrittenValues[channel] 
                    : 0.0;
                
                // AUDIT LOG: Detailed timing information
                var auditStartTime = DateTime.Now;
                System.Console.WriteLine($"[RAMP AUDIT START] {auditStartTime:HH:mm:ss.ffffff} | CH{channel} | {currentValue:F3}V → {targetValue:F3}V | Duration: {durationMs}ms");
                
                double voltageStep = (targetValue - currentValue) / steps;
                long durationNs = durationMs * 1_000_000L; // Convert to nanoseconds
                long stepNs = durationNs / steps;
                
                // HIGH-PRECISION TIMING: Use Stopwatch instead of Task.Delay
                var rampTimer = Stopwatch.StartNew();
                long stepDelayTicks = (long)(stepNs / _ticksToNanoseconds);
                
                System.Console.WriteLine($"[RAMP CONFIG] Steps: {steps} | Step Voltage: {voltageStep:F6}V | Step Duration: {stepNs / 1_000_000.0:F3}ms ({stepDelayTicks} ticks)");
                
                for (int i = 0; i < steps; i++)
                {
                    if (_disposed) return;
                    
                    // Calculate target time for this step
                    long targetTicks = (i + 1) * stepDelayTicks;
                    
                    // Update voltage
                    currentValue += voltageStep;
                    _device.Write(channel, currentValue);
                    
                    // HIGH-PRECISION WAIT: Hybrid SpinWait + Task.Delay approach
                    await HighPrecisionWaitAsync(rampTimer, targetTicks);
                    
                    // AUDIT LOG: Every 20 steps or at critical points
                    if (i % 20 == 0 || i == steps - 1)
                    {
                        long actualNs = (long)(rampTimer.ElapsedTicks * _ticksToNanoseconds);
                        long expectedNs = (i + 1) * stepNs;
                        long stepErrorNs = actualNs - expectedNs;
                        System.Console.WriteLine($"[RAMP STEP {i + 1}/{steps}] Voltage: {currentValue:F3}V | Expected: {expectedNs / 1_000_000.0:F3}ms | Actual: {actualNs / 1_000_000.0:F3}ms | Error: {stepErrorNs / 1_000_000.0:F3}ms");
                    }
                }

                // Ensure we hit the exact target value
                _device.Write(channel, targetValue);
                _lastWrittenValues[channel] = targetValue;
                
                rampTimer.Stop();
                long totalNs = (long)(rampTimer.ElapsedTicks * _ticksToNanoseconds);
                long errorNs = totalNs - durationNs;
                double errorPercent = (errorNs / (double)durationNs) * 100.0;
                
                var auditEndTime = DateTime.Now;
                System.Console.WriteLine($"[RAMP AUDIT END] {auditEndTime:HH:mm:ss.ffffff} | CH{channel} | Final: {targetValue:F3}V");
                System.Console.WriteLine($"[RAMP TIMING] Programmed: {durationMs}ms | Actual: {totalNs / 1_000_000.0:F3}ms | Error: {errorNs / 1_000_000.0:F3}ms ({errorPercent:F2}%)");
                
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
            const long SPIN_THRESHOLD_TICKS = 100_000; // ~10ms threshold for spinning
            const long TASK_DELAY_MARGIN_TICKS = 20_000; // ~2ms margin for Task.Delay imprecision
            
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
                
                _logger.Info($"Accediendo a LUT CSV: {csvPath}");
                
                // Sample rate será calculado dinámicamente más adelante
                // basado en la frecuencia deseada y samples per cycle
                
                // OPTIMIZACIÓN: Eliminar Thread.Sleep innecesarios (-30% latencia)
                // Escribir valor inicial directamente sin delays
                _device.Write(channel, offset);
                
                _logger.Info($"Starting sine wave generation on channel {channel}: {frequency}Hz, {amplitude}V amplitude, {offset}V offset");
                
                // CRITICAL FIX: Calcular samples per cycle razonable y sample rate dinámico
                // Para frecuencias bajas, usar menos samples (100-200)
                // Para frecuencias altas, usar más samples (500-1000)
                int samplesPerCycle;
                if (frequency < 10) {
                    samplesPerCycle = 100;  // Frecuencias muy bajas: 100 samples
                } else if (frequency < 50) {
                    samplesPerCycle = 200;  // Frecuencias bajas: 200 samples
                } else if (frequency < 500) {
                    samplesPerCycle = 500;  // Frecuencias medias: 500 samples
                } else {
                    samplesPerCycle = 1000; // Frecuencias altas: 1000 samples
                }
                
                // Calcular sample rate dinámico basado en frecuencia deseada
                // Esto asegura que la frecuencia real coincida con la solicitada
                double sampleRate = frequency * samplesPerCycle;
                
                _logger.Info($"Using {samplesPerCycle} samples per cycle at {sampleRate:F0} samples/sec for {frequency}Hz signal");
                
                // Inicializar el temporizador de alta precisión
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
                
                // Calcular ticks por muestra con precisión
                long ticksPerSample = (long)(System.Diagnostics.Stopwatch.Frequency / sampleRate);
                
                // Leer los valores de la LUT desde el archivo CSV
                // Nota: Leemos el archivo una vez al inicio para obtener el tamaño total
                int lutSize = File.ReadLines(csvPath).Count() - 1; // Restar 1 para la cabecera
                _logger.Info($"CSV LUT contiene {lutSize} valores");
                
                // Cargar todas las líneas del CSV en un array para acceso más rápido
                string[] lutLines = File.ReadAllLines(csvPath);
                
                // Generar la señal continuamente hasta que se cancele
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Reiniciar temporizador para cada ciclo completo
                    // Esto evita la acumulación de errores de temporización
                    long cycleStartTicks = stopwatch.ElapsedTicks;
                    
                    // Generar exactamente un ciclo completo
                    for (int i = 0; i < samplesPerCycle; i++)
                    {
                        // Calcular la fase actual (0.0 a 1.0)
                        double phase = (double)i / samplesPerCycle;
                        
                        // Calcular el índice en la LUT basado en la fase
                        int lutIndex = (int)(phase * lutSize);
                        lutIndex = Math.Max(0, Math.Min(lutIndex, lutSize - 1)); // Asegurar que esté en rango
                        
                        // Leer el valor directamente del array de líneas CSV para este índice
                        // Nota: Sumamos 1 para saltarnos la línea de cabecera
                        string line = lutLines[lutIndex + 1];
                        
                        // Procesar la línea CSV para obtener el valor
                        double normalizedValue = 0.5; // Valor predeterminado en caso de error
                        if (!string.IsNullOrEmpty(line))
                        {
                            string[] parts = line.Split(',');
                            if (parts.Length >= 2 && ushort.TryParse(parts[1], out ushort value))
                            {
                                normalizedValue = value / 65535.0; // Normalizar a [0,1]
                            }
                        }
                        
                        // Convertir a voltaje de salida (aplicando amplitud y offset)
                        double sineValue = (normalizedValue * 2.0) - 1.0; // Convertir [0,1] a [-1,1]
                        double outputVoltage = Math.Max(0.0, Math.Min(10.0, (sineValue * amplitude) + offset));
                        
                        // Escribir al DAC
                        _device.Write(channel, outputVoltage);
                        
                        // Calcular el tiempo exacto para la siguiente muestra
                        long targetTicks = cycleStartTicks + (i + 1) * ticksPerSample;
                        
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
                    
                    // Registrar estadísticas cada segundo
                    if (stopwatch.ElapsedMilliseconds > 1000)
                    {
                        double cyclesPerSecond = stopwatch.ElapsedTicks / (ticksPerSample * samplesPerCycle);
                        _logger.Debug($"Signal stats: {cyclesPerSecond:F2} Hz actual frequency");
                        stopwatch.Restart();
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
                // Asegurar que el GC se reactiva incluso si hay excepciones
                try { GC.EndNoGCRegion(); } catch { }
                
                // Restablecer afinidad de CPU al salir
                try { Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0xFFFF; } catch { }
            }
        }
        #endregion
    }
}

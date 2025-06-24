using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.CompilerServices;
using Automation.BDaq;

namespace LAMP_DAQ_Control_v0._8.Core
{
    /// <summary>
    /// Generador de señales optimizado para la tarjeta DAQ.
    /// Implementa generación precisa de señales senoidales usando LUTs.
    /// </summary>
    public class SignalGenerator : IDisposable
    {
        // Componentes principales
        private readonly InstantAoCtrl _aoCtrl;
        private Thread _signalThread;
        private bool _isRunning;
        private readonly object _lock = new object();
        private double _currentValue;
        
        // Configuración de señal
        public int Channel { get; private set; }
        public double Frequency { get; private set; }
        public double Amplitude { get; private set; }
        public double Offset { get; private set; }
        public bool IsRunning => _isRunning;
        
        // Obtener rango actual del canal
        public ValueRange CurrentRange 
        {
            get 
            {
                try
                {
                    if (_aoCtrl?.Channels == null || Channel < 0 || Channel >= _aoCtrl.Channels.Count())
                        return ValueRange.V_Neg10To10;
                    return _aoCtrl.Channels[Channel]?.ValueRange ?? ValueRange.V_Neg10To10;
                }
                catch
                {
                    return ValueRange.V_Neg10To10;
                }
            }
        }

        public SignalGenerator(InstantAoCtrl aoCtrl)
        {
            _aoCtrl = aoCtrl ?? throw new ArgumentNullException(nameof(aoCtrl));
        }

        /// <summary>
        /// Inicia la generación de señal senoidal usando LUT desde archivo
        /// </summary>
        public void Start(int channel, double frequency, double amplitude, double offset)
        {
            Stop();
            
            if (channel < 0 || channel >= _aoCtrl.Channels.Count())
                throw new ArgumentOutOfRangeException(nameof(channel), "Canal no válido");
                
            if (frequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequency), "La frecuencia debe ser mayor que cero");
            
            Channel = channel;
            Frequency = frequency;
            Amplitude = amplitude;
            Offset = offset;
            
            Console.WriteLine($"Iniciando señal senoidal usando LUT: Canal={channel}, Freq={frequency}Hz, Amp={amplitude}V, Offset={offset}V");
            Console.WriteLine($"Archivo LUT: {SignalLUTs.SinLUT.SourceFileName}, Tamaño={SignalLUTs.SinLUT.Size}");
            
            // Iniciar generación
            _isRunning = true;
            _signalThread = new Thread(GenerateSignal)
            {
                Priority = ThreadPriority.Highest,
                IsBackground = true
            };
            _signalThread.Start();
            
            Console.WriteLine($"Señal senoidal iniciada en canal {channel} - {frequency} Hz");
        }

        /// <summary>
        /// Establece un valor DC constante en el canal
        /// </summary>
        public void SetDcValue(int channel, double value)
        {
            if (_aoCtrl?.Channels == null)
                throw new InvalidOperationException("El controlador no está inicializado");
                
            if (channel < 0 || channel >= _aoCtrl.Channels.Count())
                throw new ArgumentOutOfRangeException(nameof(channel));
                
            var range = _aoCtrl.Channels[channel].ValueRange;
            value = Math.Max(GetMinValue(range), Math.Min(GetMaxValue(range), value));
            
            lock (_lock)
            {
                _aoCtrl.Write(channel, value);
            }
        }
        
        /// <summary>
        /// Genera una rampa de valor desde el valor actual hasta el valor final
        /// </summary>
        public async Task GenerateRamp(int channel, double endValue, int rampTimeMs)
        {
            if (rampTimeMs <= 0)
            {
                SetDcValue(channel, endValue);
                return;
            }
            
            double startValue;
            lock (_lock)
            {
                Channel = channel;
                startValue = _currentValue;
            }
            
            await Task.Run(() => {
                try
                {
                    var range = _aoCtrl.Channels[channel].ValueRange;
                    var startTime = DateTime.Now;
                    var endTime = startTime.AddMilliseconds(rampTimeMs);
                    
                    while (DateTime.Now < endTime)
                    {
                        double progress = Math.Min(1, (DateTime.Now - startTime).TotalMilliseconds / rampTimeMs);
                        double value = startValue + (endValue - startValue) * progress;
                        value = Math.Max(GetMinValue(range), Math.Min(GetMaxValue(range), value));
                        
                        _aoCtrl.Write(channel, value);
                        _currentValue = value;
                        
                        Thread.Sleep(5); // Intervalo más corto para mayor precisión
                    }
                    
                    // Asegurar valor final exacto
                    _aoCtrl.Write(channel, endValue);
                    _currentValue = endValue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en rampa: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Detiene la generación de señal
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _signalThread?.Join(500);
            _signalThread = null;
        }

        /// <summary>
        /// Genera la señal senoidal usando LUT desde archivo y temporización precisa
        /// </summary>
        private void GenerateSignal()
        {
            try
            {
                if (_aoCtrl?.Channels == null || Channel < 0 || Channel >= _aoCtrl.Channels.Count())
                {
                    Console.WriteLine("Error: Canal no válido");
                    return;
                }
                
                // Obtener límites del rango
                var range = CurrentRange;
                double minValue = GetMinValue(range);
                double maxValue = GetMaxValue(range);
                double valueSpan = maxValue - minValue;
                
                // Calcular escalado entre valores LUT (0-65535) y rango de voltaje
                double scale = valueSpan / 65535.0;
                
                // Fase y temporización
                double phase = 0.0;
                
                // Usar Stopwatch para precisión temporal
                var timer = new System.Diagnostics.Stopwatch();
                timer.Start();
                long lastTicks = timer.ElapsedTicks;
                
                Console.WriteLine($"Generando señal senoidal a {Frequency} Hz usando LUT desde archivo");
                
                while (_isRunning)
                {
                    try
                    {
                        // Obtener valor directamente de la LUT usando la fase normalizada
                        ushort rawValue = SignalLUTs.SinLUT.GetValueRaw(phase);
                        
                        // El valor de la LUT ya está en formato bipolar (0-65535) donde:
                        // 0 => valor mínimo (-1.0)
                        // 32768 => valor medio (0.0)
                        // 65535 => valor máximo (+1.0)
                        
                        // Convertir de formato 16-bit (0-65535) a forma de onda normalizada (-1.0 a +1.0)
                        double normalizedValue = (rawValue - 32768.0) / 32767.0;
                        
                        // Aplicar amplitud pico a pico y offset
                        double scaledValue = normalizedValue * (Amplitude / 2.0) + Offset;
                        
                        // Limitar al rango válido del DAQ
                        scaledValue = Math.Max(minValue, Math.Min(maxValue, scaledValue));
                        
                        // Mostrar valores de diagnóstico ocasionalmente
                        if (phase < 0.001 || Math.Abs(phase - 0.25) < 0.001 || 
                            Math.Abs(phase - 0.5) < 0.001 || Math.Abs(phase - 0.75) < 0.001)
                        {
                            Console.WriteLine($"Fase={phase:F3}, raw={rawValue}, norm={normalizedValue:F4}, volts={scaledValue:F4}V");
                        }
                        
                        // Enviar valor al DAQ
                        _aoCtrl.Write(Channel, scaledValue);
                        
                        // Calcular tiempo transcurrido con alta precisión
                        long currentTicks = timer.ElapsedTicks;
                        double elapsed = (double)(currentTicks - lastTicks) / System.Diagnostics.Stopwatch.Frequency;
                        lastTicks = currentTicks;
                        
                        // Actualizar fase (0.0 - 1.0) según tiempo real transcurrido
                        phase += Frequency * elapsed;
                        phase = phase - Math.Floor(phase); // Mantener entre 0.0 y 1.0
                        
                        // Temporización adaptativa para no saturar CPU
                        int waitTime = Frequency > 500 ? 0 : Frequency > 100 ? 1 : 2;
                        if (waitTime > 0) Thread.Sleep(waitTime);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error en ciclo de generación: {ex.Message}");
                        Thread.Sleep(100); // Evitar bucle rápido en caso de error
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en generación senoidal: {ex.Message}");
                Stop();
            }
        }



        /// <summary>
        /// Obtiene el valor máximo del rango
        /// </summary>
        public double GetMaxValue(ValueRange range)
        {
            return range == ValueRange.V_Neg10To10 ? 10 : 20;
        }
        
        /// <summary>
        /// Obtiene el valor mínimo del rango
        /// </summary>
        public double GetMinValue(ValueRange range)
        {
            return range == ValueRange.V_Neg10To10 ? -10 : 
                   range == ValueRange.mA_0To20 ? 0 : 4;
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();
                // Devolver todos los canales a un valor seguro
                try
                {
                    if (_aoCtrl?.Channels != null)
                    {
                        for (int i = 0; i < _aoCtrl.Channels.Count(); i++)
                        {
                            var range = _aoCtrl.Channels[i].ValueRange;
                            _aoCtrl.Write(i, range == ValueRange.mA_4To20 ? 4 : 0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al limpiar canales: {ex.Message}");
                }
            }
        }
        
        ~SignalGenerator()
        {
            Dispose(false);
        }

        /// <summary>
        /// Establece el rango para un canal
        /// </summary>
        public void SetChannelRange(int channel, ValueRange range)
        {
            if (_aoCtrl?.Channels == null || channel < 0 || channel >= _aoCtrl.Channels.Count())
                throw new ArgumentException("Canal no válido");
                
            _aoCtrl.Channels[channel].ValueRange = range;
        }

        /// <summary>
        /// Establece un valor DC de forma asíncrona
        /// </summary>
        public async Task SetDcValueAsync(int channel, double value)
        {
            await Task.Run(() => SetDcValue(channel, value));
        }
        
        /// <summary>
        /// Establece un valor DC con rampa de forma asíncrona
        /// </summary>
        /// <param name="channel">Canal a utilizar</param>
        /// <param name="targetValue">Valor objetivo</param>
        /// <param name="durationMs">Duración de la rampa en milisegundos</param>
        public async Task SetDcValueAsync(int channel, double targetValue, int durationMs)
        {
            if (durationMs <= 0)
            {
                await SetDcValueAsync(channel, targetValue);
            }
            else
            {
                await GenerateRamp(channel, targetValue, durationMs);
            }
        }

        /// <summary>
        /// Resetea todos los canales a 0 o 4mA según su configuración
        /// </summary>
        public void ResetAllOutputs()
        {
            try
            {
                if (_aoCtrl?.Channels == null) return;
                
                for (int i = 0; i < _aoCtrl.Channels.Count(); i++)
                {
                    var range = _aoCtrl.Channels[i].ValueRange;
                    double value = range == ValueRange.mA_4To20 ? 4 : 0;
                    _aoCtrl.Write(i, value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al resetear salidas: {ex.Message}");
            }
        }
    }
}
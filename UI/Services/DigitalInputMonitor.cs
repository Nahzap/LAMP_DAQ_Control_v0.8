using System;
using System.IO;
using System.Timers;
using Automation.BDaq;

namespace LAMP_DAQ_Control_v0_8.UI.Services
{
    /// <summary>
    /// Lee entradas digitales en tiempo real desde la PCI-1735U
    /// Usa un Timer para polling periódico de los puertos digitales
    /// </summary>
    public class DigitalInputMonitor : IDisposable
    {
        private InstantDiCtrl _diCtrl;
        private Timer _readTimer;
        private int _deviceNumber;
        private bool _isMonitoring;
        private byte[] _lastState;
        private byte[] _readBuffer; // OPTIMIZACIÓN: Buffer reutilizable para reducir allocations
        private readonly object _lockObject = new object();
        
        public event EventHandler<DigitalDataEventArgs> DataReceived;
        public event EventHandler<ErrorEventArgs> ErrorOccurred;
        
        public bool IsMonitoring => _isMonitoring;
        public int IntervalMs { get; private set; }
        
        public DigitalInputMonitor()
        {
            _diCtrl = new InstantDiCtrl();
            _lastState = new byte[4]; // 4 puertos
            _readBuffer = new byte[4]; // OPTIMIZACIÓN: Pre-alocar buffer para reutilizar
        }
        
        /// <summary>
        /// Inicia el monitoreo de entradas digitales
        /// </summary>
        /// <param name="deviceNumber">Número del dispositivo (Board ID)</param>
        /// <param name="intervalMs">Intervalo de lectura en milisegundos (default: 50ms = 20Hz)</param>
        public void StartMonitoring(int deviceNumber, int intervalMs = 50)
        {
            lock (_lockObject)
            {
                if (_isMonitoring)
                {
                    throw new InvalidOperationException("El monitoreo ya está activo");
                }
                
                // OPTIMIZACIÓN: Validar intervalo mínimo para evitar overhead
                if (intervalMs < 10)
                {
                    intervalMs = 10; // Mínimo absoluto: 100 Hz
                    System.Diagnostics.Debug.WriteLine($"⚠️ Intervalo ajustado a mínimo: 10ms (valor: {intervalMs}ms)");
                }
                else if (intervalMs < 50)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Advertencia: Intervalo {intervalMs}ms puede causar uso alto de CPU. Recomendado: ≥50ms");
                }
                
                _deviceNumber = deviceNumber;
                IntervalMs = intervalMs;
                
                try
                {
                    // Buscar el DeviceNumber correcto para el Board ID especificado
                    int actualDeviceNumber = -1;
                    for (int i = 0; i < _diCtrl.SupportedDevices.Count; i++)
                    {
                        var deviceInfo = _diCtrl.SupportedDevices[i];
                        if (deviceInfo.DeviceNumber == deviceNumber || 
                            deviceInfo.Description.Contains($"BID#{deviceNumber}"))
                        {
                            actualDeviceNumber = deviceInfo.DeviceNumber;
                            break;
                        }
                    }
                    
                    if (actualDeviceNumber == -1)
                    {
                        throw new Exception($"No se encontró dispositivo digital con Board ID {deviceNumber}");
                    }
                    
                    // Seleccionar dispositivo con el DeviceNumber correcto
                    _diCtrl.SelectedDevice = new DeviceInformation(actualDeviceNumber);
                    
                    // Crear y configurar timer
                    _readTimer = new Timer(intervalMs);
                    _readTimer.Elapsed += OnTimerElapsed;
                    _readTimer.AutoReset = true;
                    _readTimer.Start();
                    
                    _isMonitoring = true;
                    
                    // Hacer primera lectura inmediata
                    ReadPorts();
                }
                catch (Exception ex)
                {
                    _isMonitoring = false;
                    _readTimer?.Dispose();
                    _readTimer = null;
                    
                    ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex));
                    throw new Exception($"Error al iniciar monitoreo: {ex.Message}", ex);
                }
            }
        }
        
        /// <summary>
        /// Detiene el monitoreo de entradas digitales
        /// </summary>
        public void StopMonitoring()
        {
            lock (_lockObject)
            {
                if (!_isMonitoring)
                    return;
                    
                _isMonitoring = false;
                
                if (_readTimer != null)
                {
                    _readTimer.Stop();
                    _readTimer.Dispose();
                    _readTimer = null;
                }
            }
        }
        
        /// <summary>
        /// Cambia el intervalo de lectura (frecuencia)
        /// </summary>
        public void ChangeInterval(int newIntervalMs)
        {
            if (!_isMonitoring)
                throw new InvalidOperationException("El monitoreo no está activo");
                
            lock (_lockObject)
            {
                IntervalMs = newIntervalMs;
                if (_readTimer != null)
                {
                    _readTimer.Interval = newIntervalMs;
                }
            }
        }
        
        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            ReadPorts();
        }
        
        private void ReadPorts()
        {
            try
            {
                // OPTIMIZACIÓN: Reutilizar buffer existente en lugar de alocar nuevo
                ErrorCode result = _diCtrl.Read(0, 4, _readBuffer);
                
                if (result == ErrorCode.Success)
                {
                    // Verificar si cambió el estado (opcional - podemos emitir siempre)
                    bool hasChanged = false;
                    for (int i = 0; i < 4; i++)
                    {
                        if (_readBuffer[i] != _lastState[i])
                        {
                            hasChanged = true;
                            break;
                        }
                    }
                    
                    // Actualizar estado y emitir evento
                    // Emitimos siempre para mantener gráficos actualizados
                    lock (_lockObject)
                    {
                        Array.Copy(_readBuffer, _lastState, 4);
                    }
                    
                    // OPTIMIZACIÓN: Clonar solo cuando se necesita enviar
                    DataReceived?.Invoke(this, new DigitalDataEventArgs
                    {
                        PortData = (byte[])_readBuffer.Clone(),
                        Timestamp = DateTime.Now,
                        HasChanged = hasChanged
                    });
                }
                else
                {
                    ErrorOccurred?.Invoke(this, new ErrorEventArgs(
                        new Exception($"Error al leer DI: {result}")
                    ));
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex));
            }
        }
        
        /// <summary>
        /// Obtiene el estado actual de los puertos sin esperar al siguiente ciclo
        /// </summary>
        public byte[] GetCurrentState()
        {
            lock (_lockObject)
            {
                return (byte[])_lastState.Clone();
            }
        }
        
        /// <summary>
        /// Obtiene el estado de un bit específico
        /// </summary>
        /// <param name="port">Puerto (0-3)</param>
        /// <param name="bit">Bit (0-7)</param>
        public bool GetBitState(int port, int bit)
        {
            if (port < 0 || port > 3)
                throw new ArgumentOutOfRangeException(nameof(port), "Puerto debe estar entre 0 y 3");
            if (bit < 0 || bit > 7)
                throw new ArgumentOutOfRangeException(nameof(bit), "Bit debe estar entre 0 y 7");
                
            lock (_lockObject)
            {
                return (_lastState[port] & (1 << bit)) != 0;
            }
        }
        
        public void Dispose()
        {
            StopMonitoring();
            
            if (_diCtrl != null)
            {
                _diCtrl.Dispose();
                _diCtrl = null;
            }
        }
    }
    
    /// <summary>
    /// Argumentos del evento de datos digitales
    /// </summary>
    public class DigitalDataEventArgs : EventArgs
    {
        /// <summary>
        /// Datos de los 4 puertos (8 bits cada uno)
        /// </summary>
        public byte[] PortData { get; set; }
        
        /// <summary>
        /// Timestamp de la lectura
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Indica si hubo cambio respecto a lectura anterior
        /// </summary>
        public bool HasChanged { get; set; }
        
        /// <summary>
        /// Obtiene el estado de un bit específico
        /// </summary>
        public bool GetBit(int port, int bit)
        {
            if (port < 0 || port >= PortData.Length)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (bit < 0 || bit > 7)
                throw new ArgumentOutOfRangeException(nameof(bit));
                
            return (PortData[port] & (1 << bit)) != 0;
        }
        
        /// <summary>
        /// Convierte el estado de un puerto a array de bools
        /// </summary>
        public bool[] GetPortBits(int port)
        {
            if (port < 0 || port >= PortData.Length)
                throw new ArgumentOutOfRangeException(nameof(port));
                
            bool[] bits = new bool[8];
            for (int i = 0; i < 8; i++)
            {
                bits[i] = (PortData[port] & (1 << i)) != 0;
            }
            return bits;
        }
    }
}

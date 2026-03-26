using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Engine;
using LAMP_DAQ_Control_v0_8.UI.Models;
using LAMP_DAQ_Control_v0_8.UI.Services;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels
{
    /// <summary>
    /// ViewModel para monitoreo de dispositivos digitales (PCI-1735U)
    /// </summary>
    public class DigitalMonitorViewModel : ViewModelBase, IDisposable
    {
        private readonly DigitalInputMonitor _monitor;
        private DaqEngine _engine; // High-performance engine (optional)
        private DispatcherTimer _uiRefreshTimer; // UI refresh when using engine mode
        private DAQDevice _currentDevice;
        private bool _isMonitoring;
        private int _readFrequency;
        private bool _useEngineMode;
        private string _monitoringMode;
        private ObservableCollection<bool> _port0Bits;
        private ObservableCollection<bool> _port1Bits;
        private ObservableCollection<bool> _port2Bits;
        private ObservableCollection<bool> _port3Bits;
        
        // Propiedades
        public bool IsMonitoring
        {
            get => _isMonitoring;
            set
            {
                if (SetProperty(ref _isMonitoring, value))
                {
                    // Notificar que los comandos deben reevaluarse
                    (StartMonitoringCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StopMonitoringCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        
        public int ReadFrequency
        {
            get => _readFrequency;
            set
            {
                // OPTIMIZACIÓN: Validar rango (5-100 Hz for UI refresh)
                int validatedValue = value;
                if (validatedValue < 5) 
                {
                    validatedValue = 5;
                    System.Diagnostics.Debug.WriteLine($"Frecuencia ajustada a mínimo: 5 Hz (valor ingresado: {value} Hz)");
                }
                if (validatedValue > 100) 
                {
                    validatedValue = 100;
                    System.Diagnostics.Debug.WriteLine($"Frecuencia ajustada a máximo: 100 Hz (valor ingresado: {value} Hz)");
                }
                
                if (SetProperty(ref _readFrequency, validatedValue))
                {
                    if (IsMonitoring)
                    {
                        try
                        {
                            if (_useEngineMode && _uiRefreshTimer != null)
                            {
                                // Engine mode: adjust UI refresh rate only (hardware polls continuously)
                                _uiRefreshTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / validatedValue);
                            }
                            else
                            {
                                int intervalMs = 1000 / validatedValue;
                                _monitor.ChangeInterval(intervalMs);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Error al cambiar frecuencia:\n\n{ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Display string showing current monitoring mode.
        /// </summary>
        public string MonitoringMode
        {
            get => _monitoringMode;
            set => SetProperty(ref _monitoringMode, value);
        }
        
        // Estados de los bits por puerto (8 bits cada uno)
        public ObservableCollection<bool> Port0Bits
        {
            get => _port0Bits;
            set => SetProperty(ref _port0Bits, value);
        }
        
        public ObservableCollection<bool> Port1Bits
        {
            get => _port1Bits;
            set => SetProperty(ref _port1Bits, value);
        }
        
        public ObservableCollection<bool> Port2Bits
        {
            get => _port2Bits;
            set => SetProperty(ref _port2Bits, value);
        }
        
        public ObservableCollection<bool> Port3Bits
        {
            get => _port3Bits;
            set => SetProperty(ref _port3Bits, value);
        }
        
        // Commands
        public ICommand StartMonitoringCommand { get; }
        public ICommand StopMonitoringCommand { get; }
        
        public DigitalMonitorViewModel()
        {
            _monitor = new DigitalInputMonitor();
            _readFrequency = 10; // 10 Hz por defecto (OPTIMIZADO: -50% CPU usage)
            
            // Inicializar colecciones de bits
            Port0Bits = new ObservableCollection<bool>(new bool[8]);
            Port1Bits = new ObservableCollection<bool>(new bool[8]);
            Port2Bits = new ObservableCollection<bool>(new bool[8]);
            Port3Bits = new ObservableCollection<bool>(new bool[8]);
            
            // Suscribirse a eventos
            _monitor.DataReceived += OnDataReceived;
            _monitor.ErrorOccurred += OnErrorOccurred;
            
            // Configurar comandos
            StartMonitoringCommand = new RelayCommand(StartMonitoring, () => !IsMonitoring);
            StopMonitoringCommand = new RelayCommand(StopMonitoring, () => IsMonitoring);
        }
        
        public void Initialize(DAQDevice device)
        {
            // Detener cualquier monitoreo anterior
            if (IsMonitoring)
            {
                StopMonitoring();
            }
            
            _currentDevice = device;
            MonitoringMode = "Listo";
        }

        /// <summary>
        /// Sets the DaqEngine reference for high-speed monitoring mode.
        /// When set, StartMonitoring will use the engine's HighSpeedInputPoller
        /// instead of the Timer-based DigitalInputMonitor.
        /// </summary>
        public void SetEngine(DaqEngine engine)
        {
            _engine = engine;
        }
        
        private void StartMonitoring()
        {
            if (_currentDevice == null)
            {
                MessageBox.Show(
                    "No hay dispositivo seleccionado.\n\nPor favor seleccione PCI-1735U del panel lateral.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                // Decide monitoring mode: Engine (high-speed) or Legacy (Timer-based)
                if (_engine != null && _engine.IsRunning && _engine.HasDigital)
                {
                    StartEngineMonitoring();
                }
                else
                {
                    StartLegacyMonitoring();
                }
            }
            catch (Exception ex)
            {
                IsMonitoring = false;
                MonitoringMode = "Error";
                MessageBox.Show(
                    $"Error al iniciar monitoreo:\n\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}",
                    "Error de Monitoreo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Starts engine-based monitoring: HighSpeedInputPoller runs on dedicated thread,
        /// UI refreshes from StateGrid at the configured ReadFrequency.
        /// Hardware polling is continuous (no Timer jitter).
        /// </summary>
        private void StartEngineMonitoring()
        {
            _useEngineMode = true;

            // Create UI refresh timer that reads from StateGrid
            _uiRefreshTimer = new DispatcherTimer();
            _uiRefreshTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / ReadFrequency);
            _uiRefreshTimer.Tick += OnEngineRefreshTick;
            _uiRefreshTimer.Start();

            IsMonitoring = true;
            MonitoringMode = $"Engine (continuo, UI: {ReadFrequency}Hz)";

            MessageBox.Show(
                $"Monitoreo de alta velocidad iniciado\nModo: Engine (polling continuo)\nUI Refresh: {ReadFrequency} Hz\nDispositivo: {_currentDevice.Name}",
                "Monitoreo Engine Activo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Starts legacy Timer-based monitoring (original behavior).
        /// </summary>
        private void StartLegacyMonitoring()
        {
            _useEngineMode = false;
            int intervalMs = 1000 / ReadFrequency;
            _monitor.StartMonitoring(_currentDevice.DeviceNumber, intervalMs);

            IsMonitoring = true;
            MonitoringMode = $"Legacy (Timer: {ReadFrequency}Hz)";

            MessageBox.Show(
                $"Monitoreo iniciado correctamente\nModo: Legacy (Timer)\nFrecuencia: {ReadFrequency} Hz\nDispositivo: {_currentDevice.Name}",
                "Monitoreo Activo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// UI refresh callback when using engine mode.
        /// Reads 32-bit state from StateGrid and updates port bit collections.
        /// </summary>
        private void OnEngineRefreshTick(object sender, EventArgs e)
        {
            if (_engine == null || !_engine.IsRunning || _engine.StateGrid == null)
                return;

            uint state = _engine.StateGrid.ActiveInputMask;

            UpdatePortBits(Port0Bits, (byte)(state & 0xFF));
            UpdatePortBits(Port1Bits, (byte)((state >> 8) & 0xFF));
            UpdatePortBits(Port2Bits, (byte)((state >> 16) & 0xFF));
            UpdatePortBits(Port3Bits, (byte)((state >> 24) & 0xFF));
        }
        
        private void StopMonitoring()
        {
            try
            {
                if (_useEngineMode)
                {
                    // Stop UI refresh timer (engine poller continues independently)
                    if (_uiRefreshTimer != null)
                    {
                        _uiRefreshTimer.Stop();
                        _uiRefreshTimer.Tick -= OnEngineRefreshTick;
                        _uiRefreshTimer = null;
                    }
                }
                else
                {
                    _monitor.StopMonitoring();
                }

                IsMonitoring = false;
                _useEngineMode = false;
                MonitoringMode = "Detenido";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al detener monitoreo:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void OnDataReceived(object sender, DigitalDataEventArgs e)
        {
            // Actualizar UI en el thread de UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdatePortBits(Port0Bits, e.PortData[0]);
                UpdatePortBits(Port1Bits, e.PortData[1]);
                UpdatePortBits(Port2Bits, e.PortData[2]);
                UpdatePortBits(Port3Bits, e.PortData[3]);
            });
        }
        
        private void UpdatePortBits(ObservableCollection<bool> portBits, byte portValue)
        {
            for (int i = 0; i < 8; i++)
            {
                bool bitValue = (portValue & (1 << i)) != 0;
                portBits[i] = bitValue;
            }
        }
        
        private void OnErrorOccurred(object sender, ErrorEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"Error en monitoreo:\n\n{e.GetException().Message}",
                    "Error de Monitoreo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
        
        public void Dispose()
        {
            if (IsMonitoring)
            {
                try { StopMonitoring(); } 
                catch { /* Ignorar errores en dispose */ }
            }
            
            _monitor?.Dispose();
            _engine = null; // Don't dispose — owned by DAQController
        }
    }
}

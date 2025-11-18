using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
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
        private DAQDevice _currentDevice;
        private bool _isMonitoring;
        private int _readFrequency;
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
                // OPTIMIZACIÓN: Validar rango (5-100 Hz)
                int validatedValue = value;
                if (validatedValue < 5) 
                {
                    validatedValue = 5;
                    System.Diagnostics.Debug.WriteLine($"⚠️ Frecuencia ajustada a mínimo: 5 Hz (valor ingresado: {value} Hz)");
                }
                if (validatedValue > 100) 
                {
                    validatedValue = 100;
                    System.Diagnostics.Debug.WriteLine($"⚠️ Frecuencia ajustada a máximo: 100 Hz (valor ingresado: {value} Hz)");
                }
                
                if (SetProperty(ref _readFrequency, validatedValue))
                {
                    if (IsMonitoring)
                    {
                        try
                        {
                            int intervalMs = 1000 / validatedValue; // Convertir Hz a ms
                            _monitor.ChangeInterval(intervalMs);
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
            
            // NO iniciar automáticamente - el usuario debe hacer clic en "Iniciar"
            // StartMonitoring();
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
                // Calcular intervalo en milisegundos
                int intervalMs = 1000 / ReadFrequency;
                
                // Iniciar monitoreo con el Board ID del dispositivo
                _monitor.StartMonitoring(_currentDevice.DeviceNumber, intervalMs);
                
                // Actualizar estado
                IsMonitoring = true;
                
                // Mensaje de confirmación
                MessageBox.Show(
                    $"Monitoreo iniciado correctamente\nFrecuencia: {ReadFrequency} Hz\nDispositivo: {_currentDevice.Name}",
                    "Monitoreo Activo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                IsMonitoring = false;
                MessageBox.Show(
                    $"Error al iniciar monitoreo:\n\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}",
                    "Error de Monitoreo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void StopMonitoring()
        {
            try
            {
                _monitor.StopMonitoring();
                IsMonitoring = false;
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
                try { _monitor.StopMonitoring(); } 
                catch { /* Ignorar errores en dispose */ }
            }
            
            _monitor?.Dispose();
        }
    }
}

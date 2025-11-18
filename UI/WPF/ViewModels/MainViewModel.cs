using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.UI.Models;
using LAMP_DAQ_Control_v0_8.UI.Services;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels
{
    /// <summary>
    /// ViewModel principal de la aplicación
    /// </summary>
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly DAQController _controller;
        private readonly DeviceDetectionService _detectionService;
        private DAQDevice _selectedDevice;
        private string _statusMessage;
        private bool _isDeviceInitialized;
        
        public ObservableCollection<DAQDevice> Devices { get; set; }
        
        public DAQDevice SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    OnPropertyChanged(nameof(IsAnalogDevice));
                    OnPropertyChanged(nameof(IsDigitalDevice));
                    OnPropertyChanged(nameof(DeviceTypeText));
                    
                    StatusMessage = value != null 
                        ? $"Dispositivo seleccionado: {value.Name}" 
                        : "Ningún dispositivo seleccionado";
                    
                    if (value != null)
                    {
                        InitializeDevice(value);
                    }
                }
            }
        }
        
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        public bool IsDeviceInitialized
        {
            get => _isDeviceInitialized;
            set => SetProperty(ref _isDeviceInitialized, value);
        }
        
        // Propiedades calculadas para visibilidad de paneles
        public bool IsAnalogDevice => SelectedDevice?.DeviceType == DeviceType.Analog;
        public bool IsDigitalDevice => SelectedDevice?.DeviceType == DeviceType.Digital;
        public string DeviceTypeText => SelectedDevice != null 
            ? $"{SelectedDevice.Name} ({SelectedDevice.DeviceType})" 
            : "Ningún dispositivo seleccionado";
        
        // Child ViewModels
        public AnalogControlViewModel AnalogControl { get; }
        public DigitalMonitorViewModel DigitalMonitor { get; }
        
        // Commands
        public ICommand RefreshDevicesCommand { get; }
        public ICommand ExitCommand { get; }
        
        public MainViewModel()
        {
            // Inicializar controlador
            _controller = new DAQController();
            _detectionService = new DeviceDetectionService(new Services.ConsoleService());
            
            // Inicializar ViewModels hijos
            AnalogControl = new AnalogControlViewModel(_controller);
            DigitalMonitor = new DigitalMonitorViewModel();
            
            // Inicializar colecciones
            Devices = new ObservableCollection<DAQDevice>();
            
            // Configurar comandos
            RefreshDevicesCommand = new RelayCommand(RefreshDevices);
            ExitCommand = new RelayCommand(Exit);
            
            // Detectar dispositivos al inicio
            StatusMessage = "Iniciando...";
            RefreshDevices();
        }
        
        private void RefreshDevices()
        {
            try
            {
                StatusMessage = "Detectando dispositivos...";
                
                var detectedDevices = _detectionService.DetectDAQDevices();
                
                Devices.Clear();
                foreach (var device in detectedDevices)
                {
                    Devices.Add(device);
                }
                
                if (Devices.Count > 0)
                {
                    SelectedDevice = Devices[0];
                    StatusMessage = $"{Devices.Count} dispositivo(s) detectado(s)";
                }
                else
                {
                    StatusMessage = "No se detectaron dispositivos";
                    MessageBox.Show(
                        "No se encontraron tarjetas DAQ conectadas.\nVerifique que los drivers estén instalados.",
                        "Sin dispositivos",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al detectar dispositivos";
                MessageBox.Show(
                    $"Error al detectar dispositivos:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void InitializeDevice(DAQDevice device)
        {
            try
            {
                StatusMessage = $"Inicializando {device.Name}...";
                IsDeviceInitialized = false;
                
                // Inicializar controlador con el dispositivo
                _controller.Initialize(device.ConfigFile, device.DeviceNumber);
                
                // Inicializar ViewModel correspondiente
                if (device.DeviceType == DeviceType.Analog)
                {
                    AnalogControl.Initialize(device);
                }
                else if (device.DeviceType == DeviceType.Digital)
                {
                    DigitalMonitor.Initialize(device);
                }
                
                IsDeviceInitialized = true;
                StatusMessage = $"{device.Name} inicializado correctamente";
            }
            catch (Exception ex)
            {
                IsDeviceInitialized = false;
                StatusMessage = "Error al inicializar dispositivo";
                MessageBox.Show(
                    $"Error al inicializar dispositivo:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void Exit()
        {
            if (MessageBox.Show(
                "¿Está seguro que desea salir?",
                "Confirmar salida",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
        
        public void Dispose()
        {
            DigitalMonitor?.Dispose();
            AnalogControl?.Dispose();
            _controller?.Dispose();
        }
    }
}

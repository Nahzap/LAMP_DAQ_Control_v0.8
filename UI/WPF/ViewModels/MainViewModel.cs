using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;
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
        private readonly FileLogger _fileLogger;
        private readonly ActionLogger _actionLogger;
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
                    _actionLogger?.LogValueChange("SelectedDevice", 
                        _selectedDevice?.Name ?? "None", 
                        value?.Name ?? "None", 
                        "MainViewModel");
                    
                    OnPropertyChanged(nameof(IsAnalogDevice));
                    OnPropertyChanged(nameof(IsDigitalDevice));
                    OnPropertyChanged(nameof(DeviceTypeText));
                    
                    StatusMessage = value != null 
                        ? $"Dispositivo seleccionado: {value.Name}" 
                        : "Ningún dispositivo seleccionado";
                    
                    if (value != null)
                    {
                        _actionLogger?.LogUserAction("Device Selected", $"{value.Name} (ID: {value.DeviceNumber})");
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
        public ICommand OpenSignalManagerCommand { get; }
        
        public MainViewModel()
        {
            // Inicializar sistema de logging
            _fileLogger = new FileLogger();
            var consoleLogger = new ConsoleLogger();
            var compositeLogger = new CompositeLogger(_fileLogger, consoleLogger);
            _actionLogger = new ActionLogger(compositeLogger);
            
            _actionLogger.LogUserAction("Application Started");
            _actionLogger.LogUserAction("MainViewModel Initializing");
            
            // Inicializar controlador
            _controller = new DAQController(compositeLogger);
            _detectionService = new DeviceDetectionService(new Services.ConsoleService());
            
            // Inicializar ViewModels hijos
            AnalogControl = new AnalogControlViewModel(_controller);
            AnalogControl.SetActionLogger(_actionLogger);
            DigitalMonitor = new DigitalMonitorViewModel();
            
            // Inicializar colecciones
            Devices = new ObservableCollection<DAQDevice>();
            
            // Configurar comandos
            RefreshDevicesCommand = new RelayCommand(RefreshDevices);
            ExitCommand = new RelayCommand(Exit);
            OpenSignalManagerCommand = new RelayCommand(OpenSignalManager);
            
            // Detectar dispositivos al inicio
            StatusMessage = "Iniciando...";
            _actionLogger.LogUserAction("Refreshing Devices", "Initial device detection");
            RefreshDevices();
        }
        
        private void RefreshDevices()
        {
            _actionLogger?.LogButtonClick("RefreshDevices", "MainViewModel");
            _actionLogger?.LogUserAction("Refreshing Devices", "Scanning for DAQ devices");
            
            try
            {
                _actionLogger?.StartTiming();
                StatusMessage = "Detectando dispositivos...";
                
                var detectedDevices = _detectionService.DetectDAQDevices();
                
                _actionLogger?.StopTiming("Device Detection");
                
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
        
        public ActionLogger GetActionLogger()
        {
            return _actionLogger;
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
        
        private void OpenSignalManager()
        {
            _actionLogger?.LogButtonClick("OpenSignalManager", "MainViewModel");
            _actionLogger?.LogUserAction("Opening Signal Manager", "User opened Signal Manager window");
            
            try
            {
                // Validate that devices have been detected
                if (Devices == null || Devices.Count == 0)
                {
                    MessageBox.Show(
                        "No se han detectado dispositivos DAQ.\n\nPor favor, ejecute la detección de dispositivos primero.",
                        "Sin Dispositivos",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var view = new UI.WPF.Views.SignalManager.SignalManagerView();
                var viewModel = new UI.WPF.ViewModels.SignalManager.SignalManagerViewModel(_controller, Devices);
                view.DataContext = viewModel;
                view.Show();
                
                _actionLogger?.LogUserAction("Signal Manager Opened", $"Window displayed with {Devices.Count} devices");
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("Failed to open Signal Manager", ex);
                MessageBox.Show($"Error al abrir Signal Manager: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

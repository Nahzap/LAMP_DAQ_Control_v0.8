using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;
using LAMP_DAQ_Control_v0_8.UI.Models;
using LAMP_DAQ_Control_v0_8.UI.Services;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels
{
    /// <summary>
    /// ViewModel para control de dispositivos analógicos (PCIE-1824)
    /// </summary>
    public class AnalogControlViewModel : ViewModelBase, IDisposable
    {
        private readonly DAQController _controller;
        private readonly AnalogOutputTracker _tracker;
        private ActionLogger _actionLogger;
        private DAQDevice _currentDevice;
        
        private int _selectedChannel;
        private double _voltage;
        private double _targetVoltage;
        private int _rampDuration;
        private double _frequency;
        private double _amplitude;
        private double _offset;
        private bool _isGenerating;
        
        // Propiedades
        public int SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                var oldValue = _selectedChannel;
                bool changed = SetProperty(ref _selectedChannel, value);
                if (changed)
                {
                    _actionLogger?.LogValueChange("SelectedChannel", oldValue, value, "AnalogControlViewModel");
                    _actionLogger?.LogUserAction("Channel Selected", $"Analog Channel {value} selected for operations");
                    UpdateChartData();
                }
            }
        }
        
        public double Voltage
        {
            get => _voltage;
            set
            {
                var oldValue = _voltage;
                bool changed = SetProperty(ref _voltage, value);
                if (changed)
                {
                    _actionLogger?.LogValueChange("Voltage (DC)", oldValue, value, "AnalogControlViewModel");
                    _actionLogger?.LogUserAction("DC Voltage Value Changed", $"Voltage set to {value}V for Channel {SelectedChannel}");
                }
            }
        }
        
        public double TargetVoltage
        {
            get => _targetVoltage;
            set
            {
                var oldValue = _targetVoltage;
                if (SetProperty(ref _targetVoltage, value))
                {
                    _actionLogger?.LogValueChange("Target Voltage (Ramp)", oldValue, value, "AnalogControlViewModel");
                    _actionLogger?.LogUserAction("Ramp Target Voltage Changed", $"Target voltage set to {value}V for Channel {SelectedChannel}");
                }
            }
        }
        
        public int RampDuration
        {
            get => _rampDuration;
            set
            {
                var oldValue = _rampDuration;
                bool changed = SetProperty(ref _rampDuration, value);
                if (changed)
                {
                    _actionLogger?.LogValueChange("Ramp Duration", oldValue, value, "AnalogControlViewModel");
                    _actionLogger?.LogUserAction("Ramp Duration Changed", $"Duration set to {value}ms for Channel {SelectedChannel}");
                }
            }
        }
        
        public double Frequency
        {
            get => _frequency;
            set
            {
                var oldValue = _frequency;
                if (SetProperty(ref _frequency, value))
                {
                    _actionLogger?.LogValueChange("Signal Frequency", oldValue, value, "AnalogControlViewModel");
                    _actionLogger?.LogUserAction("Signal Frequency Changed", $"Frequency set to {value}Hz for Channel {SelectedChannel}");
                }
            }
        }
        
        public double Amplitude
        {
            get => _amplitude;
            set
            {
                var oldValue = _amplitude;
                if (SetProperty(ref _amplitude, value))
                {
                    _actionLogger?.LogValueChange("Signal Amplitude", oldValue, value, "AnalogControlViewModel");
                    _actionLogger?.LogUserAction("Signal Amplitude Changed", $"Amplitude set to {value}V for Channel {SelectedChannel}");
                }
            }
        }
        
        public double Offset
        {
            get => _offset;
            set
            {
                var oldValue = _offset;
                if (SetProperty(ref _offset, value))
                {
                    _actionLogger?.LogValueChange("Signal Offset", oldValue, value, "AnalogControlViewModel");
                    _actionLogger?.LogUserAction("Signal Offset Changed", $"Offset set to {value}V for Channel {SelectedChannel}");
                }
            }
        }
        
        public bool IsGenerating
        {
            get => _isGenerating;
            set => SetProperty(ref _isGenerating, value);
        }
        
        public ObservableCollection<DataPoint> ChartData { get; set; }
        
        // Commands
        public ICommand SetDcCommand { get; }
        public ICommand GenerateRampCommand { get; }
        public ICommand GenerateSignalCommand { get; }
        public ICommand StopSignalCommand { get; }
        public ICommand ShowInfoCommand { get; }
        
        private bool _isRampGenerating;
        
        public AnalogControlViewModel(DAQController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _tracker = new AnalogOutputTracker();
            
            ChartData = new ObservableCollection<DataPoint>();
            
            // Valores por defecto
            Voltage = 0.0;
            TargetVoltage = 5.0;
            RampDuration = 1000;
            Frequency = 100.0;
            Amplitude = 10.0;
            Offset = 0.0;
            
            // Suscribirse a eventos del tracker
            _tracker.DataRecorded += OnDataRecorded;
            
            // Configurar comandos
            SetDcCommand = new RelayCommand(SetDc, () => !IsGenerating && !_isRampGenerating);
            GenerateRampCommand = new RelayCommand(async () => await GenerateRampAsync(), () => !IsGenerating && !_isRampGenerating);
            GenerateSignalCommand = new RelayCommand(GenerateSignal, () => !IsGenerating && !_isRampGenerating);
            StopSignalCommand = new RelayCommand(StopSignal, () => IsGenerating);
            ShowInfoCommand = new RelayCommand(ShowInfo);
        }
        
        public void SetActionLogger(ActionLogger actionLogger)
        {
            _actionLogger = actionLogger;
            if (_actionLogger != null)
            {
                _actionLogger.LogUserAction("AnalogControlViewModel Logger Connected", "Action logging enabled for analog control");
            }
        }
        
        public void Initialize(DAQDevice device)
        {
            if (_actionLogger == null)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: ActionLogger is NULL in AnalogControlViewModel.Initialize!");
            }
            else
            {
                _actionLogger.LogUserAction("Initialize Analog Device", $"{device.Name} (ID: {device.DeviceNumber})");
                _actionLogger.LogUserAction("AnalogControlViewModel Ready", $"Logging enabled for analog operations on {device.Name}");
            }
            _currentDevice = device;
            _tracker.ClearAllHistory();
            ChartData.Clear();
        }
        
        private void SetDc()
        {
            _actionLogger?.LogButtonClick("SetDC", "AnalogControlViewModel");
            _actionLogger?.LogUserAction("Set DC Voltage", $"Channel: {SelectedChannel}, Voltage: {Voltage}V");
            
            try
            {
                _actionLogger?.StartTiming();
                _controller.WriteVoltage(SelectedChannel, Voltage);
                _actionLogger?.StopTiming($"Write DC {Voltage}V to Channel {SelectedChannel}");
                
                _tracker.RecordWrite(SelectedChannel, Voltage, DateTime.Now);
                _actionLogger?.LogUserAction("DC Voltage Set Successfully", $"Channel {SelectedChannel} = {Voltage}V");
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("SetDC", ex);
                MessageBox.Show(
                    $"Error al establecer DC:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private async Task GenerateRampAsync()
        {
            if (_isRampGenerating)
            {
                _actionLogger?.LogUserAction("Ramp Generation Blocked", "A ramp is already in progress");
                return;
            }
            
            _actionLogger?.LogButtonClick("GenerateRamp", "AnalogControlViewModel");
            _actionLogger?.LogUserAction("Generate Ramp", 
                $"Channel: {SelectedChannel}, Target: {TargetVoltage}V, Duration: {RampDuration}ms");
            
            _isRampGenerating = true;
            
            try
            {
                _actionLogger?.StartTiming();
                
                // CRITICAL FIX: Properly await the async operation
                await _controller.RampChannelValue(SelectedChannel, TargetVoltage, RampDuration);
                
                _actionLogger?.StopTiming($"Ramp Channel {SelectedChannel} to {TargetVoltage}V");
                
                // Registrar punto inicial y final
                _tracker.RecordWrite(SelectedChannel, Voltage, DateTime.Now);
                _tracker.RecordWrite(SelectedChannel, TargetVoltage, 
                    DateTime.Now.AddMilliseconds(RampDuration));
                    
                Voltage = TargetVoltage;
                _actionLogger?.LogUserAction("Ramp Completed", $"Channel {SelectedChannel} reached {TargetVoltage}V");
            }
            catch (ObjectDisposedException ex)
            {
                _actionLogger?.LogException("GenerateRamp - Device Disposed", ex);
                MessageBox.Show(
                    $"El dispositivo fue desconectado durante la rampa.\n\nDetalles: {ex.Message}",
                    "Error - Dispositivo Desconectado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _actionLogger?.LogException("GenerateRamp - Invalid Parameters", ex);
                MessageBox.Show(
                    $"Parámetros de rampa inválidos.\n\nDetalles: {ex.Message}",
                    "Error - Parámetros Inválidos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("GenerateRamp - Unexpected Error", ex);
                MessageBox.Show(
                    $"Error inesperado al generar rampa:\n\n{ex.GetType().Name}: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Error - Generación de Rampa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isRampGenerating = false;
            }
        }
        
        private void GenerateSignal()
        {
            _actionLogger?.LogButtonClick("GenerateSignal", "AnalogControlViewModel");
            _actionLogger?.LogUserAction("Start Signal Generation", 
                $"Channel: {SelectedChannel}, Freq: {Frequency}Hz, Amp: {Amplitude}V, Offset: {Offset}V");
            
            try
            {
                _actionLogger?.StartTiming();
                _controller.StartSignalGeneration(SelectedChannel, Frequency, Amplitude, Offset);
                _actionLogger?.StopTiming($"Start Signal on Channel {SelectedChannel}");
                
                IsGenerating = true;
                _actionLogger?.LogUserAction("Signal Generation Started", 
                    $"Channel {SelectedChannel}: {Frequency}Hz sine wave, {Amplitude}V amplitude, {Offset}V offset");
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("GenerateSignal", ex);
                MessageBox.Show(
                    $"Error al generar señal:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void StopSignal()
        {
            _actionLogger?.LogButtonClick("StopSignal", "AnalogControlViewModel");
            _actionLogger?.LogUserAction("Stop Signal Generation", $"Channel: {SelectedChannel}");
            
            try
            {
                _controller.StopSignalGeneration();
                IsGenerating = false;
                _actionLogger?.LogUserAction("Signal Generation Stopped", $"Channel {SelectedChannel}");
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("StopSignal", ex);
                MessageBox.Show(
                    $"Error al detener señal:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void ShowInfo()
        {
            _actionLogger?.LogButtonClick("ShowInfo", "AnalogControlViewModel");
            _actionLogger?.LogUserAction("Show Device Info", "Displaying device information");
            
            try
            {
                var info = _controller.GetDeviceInfo();
                _actionLogger?.LogUserAction("Device Info Retrieved", 
                    $"{info.Name}, Channels: {info.Channels}, Type: {info.DeviceType}");
                
                MessageBox.Show(
                    $"Dispositivo: {info.Name}\n" +
                    $"Canales: {info.Channels}\n" +
                    $"Tipo: {info.DeviceType}\n" +
                    $"Estado: {(info.AdditionalInfo ?? "Inicializado")}",
                    "Información del Dispositivo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("ShowInfo", ex);
                MessageBox.Show(
                    $"Error al obtener información:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void OnDataRecorded(object sender, AnalogDataEventArgs e)
        {
            if (e.Channel == SelectedChannel)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ChartData.Add(new DataPoint
                    {
                        Timestamp = e.Timestamp,
                        Value = e.Voltage
                    });
                    
                    // Mantener solo últimos 1000 puntos
                    while (ChartData.Count > 1000)
                    {
                        ChartData.RemoveAt(0);
                    }
                });
            }
        }
        
        private void UpdateChartData()
        {
            var history = _tracker.GetChannelHistory(SelectedChannel);
            ChartData.Clear();
            foreach (var point in history)
            {
                ChartData.Add(point);
            }
        }
        
        public void Dispose()
        {
            if (IsGenerating)
            {
                try { _controller.StopSignalGeneration(); } 
                catch { /* Ignorar errores en dispose */ }
            }
        }
    }
}

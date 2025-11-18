using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
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
                if (SetProperty(ref _selectedChannel, value))
                {
                    UpdateChartData();
                }
            }
        }
        
        public double Voltage
        {
            get => _voltage;
            set => SetProperty(ref _voltage, value);
        }
        
        public double TargetVoltage
        {
            get => _targetVoltage;
            set => SetProperty(ref _targetVoltage, value);
        }
        
        public int RampDuration
        {
            get => _rampDuration;
            set => SetProperty(ref _rampDuration, value);
        }
        
        public double Frequency
        {
            get => _frequency;
            set => SetProperty(ref _frequency, value);
        }
        
        public double Amplitude
        {
            get => _amplitude;
            set => SetProperty(ref _amplitude, value);
        }
        
        public double Offset
        {
            get => _offset;
            set => SetProperty(ref _offset, value);
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
            SetDcCommand = new RelayCommand(SetDc, () => !IsGenerating);
            GenerateRampCommand = new RelayCommand(GenerateRamp, () => !IsGenerating);
            GenerateSignalCommand = new RelayCommand(GenerateSignal, () => !IsGenerating);
            StopSignalCommand = new RelayCommand(StopSignal, () => IsGenerating);
            ShowInfoCommand = new RelayCommand(ShowInfo);
        }
        
        public void Initialize(DAQDevice device)
        {
            _currentDevice = device;
            _tracker.ClearAllHistory();
            ChartData.Clear();
        }
        
        private void SetDc()
        {
            try
            {
                _controller.WriteVoltage(SelectedChannel, Voltage);
                _tracker.RecordWrite(SelectedChannel, Voltage, DateTime.Now);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al establecer DC:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void GenerateRamp()
        {
            try
            {
                _controller.RampChannelValue(SelectedChannel, TargetVoltage, RampDuration);
                
                // Registrar punto inicial y final
                _tracker.RecordWrite(SelectedChannel, Voltage, DateTime.Now);
                _tracker.RecordWrite(SelectedChannel, TargetVoltage, 
                    DateTime.Now.AddMilliseconds(RampDuration));
                    
                Voltage = TargetVoltage;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al generar rampa:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void GenerateSignal()
        {
            try
            {
                _controller.StartSignalGeneration(SelectedChannel, Frequency, Amplitude, Offset);
                IsGenerating = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al generar señal:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void StopSignal()
        {
            try
            {
                _controller.StopSignalGeneration();
                IsGenerating = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al detener señal:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void ShowInfo()
        {
            try
            {
                var info = _controller.GetDeviceInfo();
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

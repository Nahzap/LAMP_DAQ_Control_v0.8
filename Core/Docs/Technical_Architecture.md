# Arquitectura Técnica - Migración WPF
## LAMP DAQ Control v0.8

**Versión**: 1.0  
**Última actualización**: 17 de Noviembre, 2025

---

## 📐 Diagrama de Arquitectura

\\\
┌─────────────────────────────────────────────────────────────┐
│                      PRESENTATION LAYER (WPF)                │
├─────────────────────────────────────────────────────────────┤
│  App.xaml                                                    │
│    └─> MainWindow.xaml                                      │
│         ├─> AnalogControlPanel.xaml                         │
│         │    └─> RealtimeChartControl.xaml (ScottPlot)      │
│         └─> DigitalControlPanel.xaml                        │
│              └─> RealtimeChartControl.xaml (ScottPlot)      │
├─────────────────────────────────────────────────────────────┤
│                      VIEWMODEL LAYER (MVVM)                  │
├─────────────────────────────────────────────────────────────┤
│  MainViewModel                                               │
│    ├─> AnalogControlViewModel                               │
│    └─> DigitalMonitorViewModel                              │
├─────────────────────────────────────────────────────────────┤
│                      SERVICE LAYER (UI)                      │
├─────────────────────────────────────────────────────────────┤
│  AnalogOutputTracker    DigitalInputMonitor                 │
│  RealtimeDataService    DeviceDetectionService              │
├─────────────────────────────────────────────────────────────┤
│                      BUSINESS LOGIC (CORE)                   │
├─────────────────────────────────────────────────────────────┤
│  DAQController                                               │
│    ├─> DeviceManager                                        │
│    ├─> ProfileManager                                       │
│    ├─> ChannelManager                                       │
│    └─> SignalGenerator                                      │
├─────────────────────────────────────────────────────────────┤
│                      HARDWARE LAYER (SDK)                    │
├─────────────────────────────────────────────────────────────┤
│  Automation.BDaq (Advantech SDK)                            │
│    ├─> InstantAoCtrl (PCIE-1824 AO)                        │
│    ├─> InstantDiCtrl (PCI-1735U DI)                        │
│    └─> InstantDoCtrl (PCI-1735U DO)                        │
└─────────────────────────────────────────────────────────────┘
\\\

---

## 🔄 Flujo de Datos

### Escritura Analógica (PCIE-1824)

\\\
Usuario → [UI] NumericUpDown (Voltaje)
          ↓
    [ViewModel] AnalogControlViewModel.SetVoltage()
          ↓
    [Command] RelayCommand ejecuta
          ↓
    [Core] DAQController.WriteVoltage(channel, voltage)
          ↓
    [Manager] ChannelManager.WriteVoltage()
          ↓
    [Manager] DeviceManager.WriteVoltage()
          ↓
    [SDK] InstantAoCtrl.Write(channel, voltage)
          ↓
    [Hardware] PCIE-1824 salida física
          ↓
    [Service] AnalogOutputTracker.RecordWrite() ← Tracking
          ↓
    [Event] DataRecorded event fired
          ↓
    [ViewModel] Actualiza ObservableCollection
          ↓
    [UI] ScottPlot actualiza gráfico
\\\

### Lectura Digital (PCI-1735U)

\\\
[Timer] DigitalInputMonitor.OnTimerElapsed() (cada 50ms)
          ↓
    [SDK] InstantDiCtrl.Read(startPort, count, buffer)
          ↓
    [Hardware] PCI-1735U lee pines físicos
          ↓
    [Service] Procesa buffer (byte[4])
          ↓
    [Event] DataReceived event fired
          ↓
    [ViewModel] DigitalMonitorViewModel.OnDataReceived()
          ↓
    [Dispatcher] Application.Current.Dispatcher.Invoke()
          ↓
    [ViewModel] Actualiza ObservableCollection
          ↓
    [UI] CheckBoxes + ScottPlot actualizan
\\\

---

## 🧩 Componentes Detallados

### 1. AnalogOutputTracker

**Ubicación**: \UI/Services/AnalogOutputTracker.cs\

\\\csharp
public class AnalogOutputTracker
{
    // Fields
    private Dictionary<int, CircularBuffer<DataPoint>> _channelBuffers;
    private const int BufferSize = 1000;
    
    // Events
    public event EventHandler<AnalogDataEventArgs> DataRecorded;
    
    // Methods
    public void RecordWrite(int channel, double voltage, DateTime timestamp)
    {
        if (!_channelBuffers.ContainsKey(channel))
            _channelBuffers[channel] = new CircularBuffer<DataPoint>(BufferSize);
            
        _channelBuffers[channel].Add(new DataPoint 
        { 
            Timestamp = timestamp, 
            Value = voltage 
        });
        
        DataRecorded?.Invoke(this, new AnalogDataEventArgs 
        { 
            Channel = channel, 
            Voltage = voltage, 
            Timestamp = timestamp 
        });
    }
    
    public DataPoint[] GetChannelHistory(int channel)
    {
        return _channelBuffers.ContainsKey(channel) 
            ? _channelBuffers[channel].ToArray() 
            : Array.Empty<DataPoint>();
    }
    
    public void ClearHistory(int channel)
    {
        if (_channelBuffers.ContainsKey(channel))
            _channelBuffers[channel].Clear();
    }
}

// Supporting classes
public class DataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

public class AnalogDataEventArgs : EventArgs
{
    public int Channel { get; set; }
    public double Voltage { get; set; }
    public DateTime Timestamp { get; set; }
}

// Circular buffer implementation
public class CircularBuffer<T>
{
    private T[] _buffer;
    private int _head;
    private int _count;
    
    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
        _head = 0;
        _count = 0;
    }
    
    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length) _count++;
    }
    
    public T[] ToArray()
    {
        T[] result = new T[_count];
        for (int i = 0; i < _count; i++)
        {
            int index = (_head - _count + i + _buffer.Length) % _buffer.Length;
            result[i] = _buffer[index];
        }
        return result;
    }
}
\\\

---

### 2. DigitalInputMonitor

**Ubicación**: \UI/Services/DigitalInputMonitor.cs\

\\\csharp
public class DigitalInputMonitor : IDisposable
{
    // Fields
    private InstantDiCtrl _diCtrl;
    private Timer _readTimer;
    private int _deviceNumber;
    private bool _isMonitoring;
    private byte[] _lastState;
    
    // Events
    public event EventHandler<DigitalDataEventArgs> DataReceived;
    public event EventHandler<ErrorEventArgs> ErrorOccurred;
    
    // Constructor
    public DigitalInputMonitor()
    {
        _diCtrl = new InstantDiCtrl();
        _lastState = new byte[4];
    }
    
    // Methods
    public void StartMonitoring(int deviceNumber, int intervalMs = 50)
    {
        if (_isMonitoring)
            throw new InvalidOperationException(\"Already monitoring\");
            
        _deviceNumber = deviceNumber;
        
        try
        {
            _diCtrl.SelectedDevice = new DeviceInformation(deviceNumber);
            
            _readTimer = new Timer(intervalMs);
            _readTimer.Elapsed += OnTimerElapsed;
            _readTimer.AutoReset = true;
            _readTimer.Start();
            
            _isMonitoring = true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex));
            throw;
        }
    }
    
    public void StopMonitoring()
    {
        _isMonitoring = false;
        _readTimer?.Stop();
        _readTimer?.Dispose();
        _readTimer = null;
    }
    
    private void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
        try
        {
            byte[] portData = new byte[4];
            ErrorCode result = _diCtrl.Read(0, 4, portData);
            
            if (result == ErrorCode.Success)
            {
                // Solo emitir evento si cambió el estado
                bool hasChanged = false;
                for (int i = 0; i < 4; i++)
                {
                    if (portData[i] != _lastState[i])
                    {
                        hasChanged = true;
                        break;
                    }
                }
                
                if (hasChanged || true) // Emitir siempre para gráficos
                {
                    Array.Copy(portData, _lastState, 4);
                    
                    DataReceived?.Invoke(this, new DigitalDataEventArgs
                    {
                        PortData = portData,
                        Timestamp = DateTime.Now
                    });
                }
            }
            else
            {
                ErrorOccurred?.Invoke(this, new ErrorEventArgs(
                    new Exception(\$\"Error reading DI: {result}\")));
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex));
        }
    }
    
    public byte[] GetCurrentState()
    {
        return (byte[])_lastState.Clone();
    }
    
    public void Dispose()
    {
        StopMonitoring();
        _diCtrl?.Dispose();
    }
}

public class DigitalDataEventArgs : EventArgs
{
    public byte[] PortData { get; set; }
    public DateTime Timestamp { get; set; }
}
\\\

---

### 3. MainViewModel

**Ubicación**: \UI/WPF/ViewModels/MainViewModel.cs\

\\\csharp
public class MainViewModel : INotifyPropertyChanged
{
    // Fields
    private readonly DAQController _controller;
    private readonly DeviceDetectionService _detectionService;
    private ObservableCollection<DAQDevice> _devices;
    private DAQDevice _selectedDevice;
    
    // Properties
    public ObservableCollection<DAQDevice> Devices
    {
        get => _devices;
        set
        {
            _devices = value;
            OnPropertyChanged();
        }
    }
    
    public DAQDevice SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            _selectedDevice = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAnalogDevice));
            OnPropertyChanged(nameof(IsDigitalDevice));
            
            if (value != null)
                InitializeDevice(value);
        }
    }
    
    public bool IsAnalogDevice => SelectedDevice?.DeviceType == DeviceType.Analog;
    public bool IsDigitalDevice => SelectedDevice?.DeviceType == DeviceType.Digital;
    
    // Child ViewModels
    public AnalogControlViewModel AnalogControl { get; }
    public DigitalMonitorViewModel DigitalMonitor { get; }
    
    // Commands
    public ICommand RefreshDevicesCommand { get; }
    public ICommand ExitCommand { get; }
    
    // Constructor
    public MainViewModel()
    {
        _controller = new DAQController();
        _detectionService = new DeviceDetectionService(new WpfUIService());
        
        AnalogControl = new AnalogControlViewModel(_controller);
        DigitalMonitor = new DigitalMonitorViewModel();
        
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        ExitCommand = new RelayCommand(Exit);
        
        RefreshDevices();
    }
    
    // Methods
    private void RefreshDevices()
    {
        var detectedDevices = _detectionService.DetectDAQDevices();
        Devices = new ObservableCollection<DAQDevice>(detectedDevices);
        
        if (Devices.Count > 0)
            SelectedDevice = Devices[0];
    }
    
    private void InitializeDevice(DAQDevice device)
    {
        try
        {
            _controller.Initialize(device.ConfigFile, device.DeviceNumber);
            
            if (device.DeviceType == DeviceType.Analog)
                AnalogControl.Initialize(device);
            else if (device.DeviceType == DeviceType.Digital)
                DigitalMonitor.Initialize(device);
        }
        catch (Exception ex)
        {
            MessageBox.Show(\$\"Error al inicializar dispositivo: {ex.Message}\",
                \"Error\", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void Exit()
    {
        Application.Current.Shutdown();
    }
    
    // INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
\\\

---

### 4. AnalogControlViewModel

**Ubicación**: \UI/WPF/ViewModels/AnalogControlViewModel.cs\

\\\csharp
public class AnalogControlViewModel : INotifyPropertyChanged
{
    // Fields
    private readonly DAQController _controller;
    private readonly AnalogOutputTracker _tracker;
    private int _selectedChannel;
    private double _voltage;
    private ObservableCollection<DataPoint> _chartData;
    
    // Properties
    public int SelectedChannel
    {
        get => _selectedChannel;
        set
        {
            _selectedChannel = value;
            OnPropertyChanged();
            UpdateChartData();
        }
    }
    
    public double Voltage
    {
        get => _voltage;
        set
        {
            _voltage = value;
            OnPropertyChanged();
        }
    }
    
    public ObservableCollection<DataPoint> ChartData
    {
        get => _chartData;
        set
        {
            _chartData = value;
            OnPropertyChanged();
        }
    }
    
    // Commands
    public ICommand SetDcCommand { get; }
    public ICommand GenerateRampCommand { get; }
    public ICommand GenerateSignalCommand { get; }
    
    // Constructor
    public AnalogControlViewModel(DAQController controller)
    {
        _controller = controller;
        _tracker = new AnalogOutputTracker();
        _chartData = new ObservableCollection<DataPoint>();
        
        _tracker.DataRecorded += OnDataRecorded;
        
        SetDcCommand = new RelayCommand(SetDc);
        GenerateRampCommand = new RelayCommand(GenerateRamp);
        GenerateSignalCommand = new RelayCommand(GenerateSignal);
    }
    
    // Methods
    public void Initialize(DAQDevice device)
    {
        // Inicialización específica del dispositivo
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
            MessageBox.Show(\$\"Error: {ex.Message}\", \"Error\",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void GenerateRamp()
    {
        // Implementación
    }
    
    private void GenerateSignal()
    {
        // Implementación
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
                    ChartData.RemoveAt(0);
            });
        }
    }
    
    private void UpdateChartData()
    {
        var history = _tracker.GetChannelHistory(SelectedChannel);
        ChartData = new ObservableCollection<DataPoint>(history);
    }
    
    // INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
\\\

---

## 🎨 Diseño de UI (XAML)

### MainWindow.xaml (Simplificado)

\\\xml
<Window x:Class=\"LAMP_DAQ_Control_v0_8.UI.WPF.Windows.MainWindow\"
        xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"
        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"
        xmlns:vm=\"clr-namespace:LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels\"
        xmlns:views=\"clr-namespace:LAMP_DAQ_Control_v0_8.UI.WPF.Views\"
        Title=\"LAMP DAQ Control v0.8\" 
        Height=\"800\" Width=\"1200\"
        WindowStartupLocation=\"CenterScreen\">
    
    <Window.DataContext>
        <vm:MainViewModel/>
    </Window.DataContext>
    
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height=\"Auto\"/>
            <RowDefinition Height=\"*\"/>
            <RowDefinition Height=\"Auto\"/>
        </Grid.RowDefinitions>
        
        <!-- Header -->
        <Border Grid.Row=\"0\" Background=\"#2C3E50\" Padding=\"10\">
            <StackPanel Orientation=\"Horizontal\">
                <TextBlock Text=\"LAMP DAQ Control v0.8\" 
                          Foreground=\"White\" 
                          FontSize=\"20\" 
                          FontWeight=\"Bold\"/>
                <Button Content=\"Refrescar\" 
                        Command=\"{Binding RefreshDevicesCommand}\"
                        Margin=\"20,0,0,0\"/>
            </StackPanel>
        </Border>
        
        <!-- Content -->
        <Grid Grid.Row=\"1\">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=\"250\"/>
                <ColumnDefinition Width=\"*\"/>
            </Grid.ColumnDefinitions>
            
            <!-- Sidebar -->
            <Border Grid.Column=\"0\" BorderBrush=\"Gray\" BorderThickness=\"0,0,1,0\">
                <StackPanel Margin=\"10\">
                    <TextBlock Text=\"Dispositivos Detectados\" 
                              FontWeight=\"Bold\" 
                              FontSize=\"14\" 
                              Margin=\"0,0,0,10\"/>
                    
                    <ListBox ItemsSource=\"{Binding Devices}\"
                            SelectedItem=\"{Binding SelectedDevice}\">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <StackPanel>
                                    <TextBlock Text=\"{Binding Name}\" FontWeight=\"Bold\"/>
                                    <TextBlock Text=\"{Binding DeviceType}\" FontSize=\"10\"/>
                                </StackPanel>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </StackPanel>
            </Border>
            
            <!-- Main Content Area -->
            <Grid Grid.Column=\"1\">
                <!-- Analog Panel -->
                <views:AnalogControlPanel 
                    DataContext=\"{Binding AnalogControl}\"
                    Visibility=\"{Binding IsAnalogDevice, 
                        Converter={StaticResource BoolToVisibilityConverter}}\"/>
                
                <!-- Digital Panel -->
                <views:DigitalControlPanel 
                    DataContext=\"{Binding DigitalMonitor}\"
                    Visibility=\"{Binding IsDigitalDevice, 
                        Converter={StaticResource BoolToVisibilityConverter}}\"/>
            </Grid>
        </Grid>
        
        <!-- Status Bar -->
        <StatusBar Grid.Row=\"2\">
            <StatusBarItem>
                <TextBlock Text=\"Listo\"/>
            </StatusBarItem>
        </StatusBar>
    </Grid>
</Window>
\\\

---

## 📊 Patrones de Diseño Utilizados

### 1. MVVM (Model-View-ViewModel)
- **Separación de responsabilidades**
- **Binding bidireccional**
- **Commands para acciones**

### 2. Observer Pattern
- **Events para notificaciones**
- **INotifyPropertyChanged**

### 3. Strategy Pattern
- **IDeviceMenuHandler** (ya existente)
- Diferentes paneles según tipo de dispositivo

### 4. Dependency Injection
- **DAQController** acepta interfaces
- Facilita testing

### 5. Repository Pattern
- **AnalogOutputTracker** actúa como repositorio de datos históricos

---

**Fin del Documento Técnico**

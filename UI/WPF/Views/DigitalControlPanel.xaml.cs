using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;
using LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Views
{
    /// <summary>
    /// Lógica de interacción para DigitalControlPanel.xaml
    /// </summary>
    public partial class DigitalControlPanel : UserControl
    {
        private InstantDoCtrl _doCtrl;
        private byte[] _portStates; // Estado actual de cada puerto (0-3)
        private ActionLogger _actionLogger;
        
        public DigitalControlPanel()
        {
            InitializeComponent();
            
            // Inicializar controlador de salida digital
            _doCtrl = new InstantDoCtrl();
            
            // Inicializar estados en 0
            _portStates = new byte[4];
            
            // Actualizar visualización inicial
            Loaded += (s, e) => UpdateOutputStatesDisplay();
        }
        
        public void SetActionLogger(ActionLogger logger)
        {
            _actionLogger = logger;
            _actionLogger?.LogUserAction("DigitalControlPanel Initialized");
        }
        
        private void UpdateOutputStatesDisplay()
        {
            var panel = OutputStatesPanel;
            if (panel == null) return;
            
            panel.Children.Clear();
            
            for (int port = 0; port < 4; port++)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                
                var label = new TextBlock 
                { 
                    Text = $"P{port}:", 
                    FontWeight = FontWeights.Bold, 
                    Width = 25 
                };
                sp.Children.Add(label);
                
                byte value = _portStates[port];
                for (int bit = 0; bit < 8; bit++)
                {
                    bool isSet = (value & (1 << bit)) != 0;
                    var border = new Border
                    {
                        Width = 18,
                        Height = 18,
                        Margin = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(2),
                        Background = new SolidColorBrush(isSet ? Color.FromRgb(46, 204, 113) : Color.FromRgb(189, 195, 199))
                    };
                    sp.Children.Add(border);
                }
                
                panel.Children.Add(sp);
            }
        }
        
        private void OnWritePortClick(object sender, RoutedEventArgs e)
        {
            _actionLogger?.LogButtonClick("WritePort", "DigitalControlPanel");
            
            try
            {
                int port = int.Parse(WritePortNumber.Text);
                byte value = byte.Parse(WritePortValue.Text);
                
                _actionLogger?.LogUserAction("Write Digital Port", $"Port: {port}, Value: {value} (0x{value:X2}, Binary: {Convert.ToString(value, 2).PadLeft(8, '0')})");
                
                if (port < 0 || port > 3)
                {
                    MessageBox.Show("El puerto debe estar entre 0 y 3", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (_doCtrl.SupportedDevices.Count > 0)
                {
                    _doCtrl.SelectedDevice = new DeviceInformation(1);
                    
                    _actionLogger?.StartTiming();
                    ErrorCode result = _doCtrl.Write(port, value);
                    _actionLogger?.StopTiming($"Digital Port Write P{port}={value}");
                    
                    if (result == ErrorCode.Success)
                    {
                        _portStates[port] = value; // Actualizar estado en memoria
                        UpdateOutputStatesDisplay(); // Actualizar visualización
                        
                        _actionLogger?.LogUserAction("Digital Port Written Successfully", 
                            $"Port {port} = {value} (0x{value:X2}, Binary: {Convert.ToString(value, 2).PadLeft(8, '0')})");
                        
                        MessageBox.Show($"Puerto {port} = {value} (0x{value:X2}) escrito correctamente", 
                            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        _actionLogger?.LogWarning($"Failed to write digital port: {result}", "DigitalControlPanel");
                        MessageBox.Show($"Error al escribir: {result}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    _actionLogger?.LogWarning("No digital devices available", "DigitalControlPanel");
                }
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("WritePort", ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnResetAllPortsClick(object sender, RoutedEventArgs e)
        {
            _actionLogger?.LogButtonClick("ResetAllPorts", "DigitalControlPanel");
            _actionLogger?.LogUserAction("Reset All Digital Ports", "Setting all 4 ports to 0");
            
            try
            {
                if (_doCtrl.SupportedDevices.Count > 0)
                {
                    _doCtrl.SelectedDevice = new DeviceInformation(1);
                    
                    _actionLogger?.StartTiming();
                    for (int port = 0; port < 4; port++)
                    {
                        ErrorCode result = _doCtrl.Write(port, 0);
                        _portStates[port] = 0; // Actualizar estado en memoria
                        _actionLogger?.LogUserAction("Port Reset", $"Port {port} = 0");
                    }
                    _actionLogger?.StopTiming("Reset All 4 Ports");
                    
                    UpdateOutputStatesDisplay(); // Actualizar visualización
                    
                    _actionLogger?.LogUserAction("All Ports Reset Successfully", "All 4 ports set to 0");
                    MessageBox.Show("Todos los puertos reseteados a 0", "Éxito", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("ResetAllPorts", ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnSetAllPortsClick(object sender, RoutedEventArgs e)
        {
            _actionLogger?.LogButtonClick("SetAllPorts", "DigitalControlPanel");
            _actionLogger?.LogUserAction("Set All Digital Ports", "Setting all 4 ports to 255 (0xFF)");
            
            try
            {
                if (_doCtrl.SupportedDevices.Count > 0)
                {
                    _doCtrl.SelectedDevice = new DeviceInformation(1);
                    
                    _actionLogger?.StartTiming();
                    for (int port = 0; port < 4; port++)
                    {
                        ErrorCode result = _doCtrl.Write(port, 255);
                        _portStates[port] = 255; // Actualizar estado en memoria
                        _actionLogger?.LogUserAction("Port Set", $"Port {port} = 255 (0xFF, all bits HIGH)");
                    }
                    _actionLogger?.StopTiming("Set All 4 Ports to 255");
                    
                    UpdateOutputStatesDisplay(); // Actualizar visualización
                    
                    _actionLogger?.LogUserAction("All Ports Set Successfully", "All 4 ports set to 255 (all bits HIGH)");
                    MessageBox.Show("Todos los puertos activados a 255", "Éxito", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("SetAllPorts", ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnBitToggle(object sender, RoutedEventArgs e)
        {
            try
            {
                var checkbox = sender as System.Windows.Controls.CheckBox;
                if (checkbox == null || checkbox.Tag == null) return;
                
                // Tag formato: "puerto,bit" ej: "0,5" = Puerto 0, Bit 5
                var parts = checkbox.Tag.ToString().Split(',');
                if (parts.Length != 2) return;
                
                int port = int.Parse(parts[0]);
                int bit = int.Parse(parts[1]);
                bool isChecked = checkbox.IsChecked ?? false;
                
                _actionLogger?.LogUserAction("Digital Bit Toggle", 
                    $"Port {port}, Bit {bit}: {(isChecked ? "ON (1)" : "OFF (0)")}");
                
                if (_doCtrl.SupportedDevices.Count > 0)
                {
                    _doCtrl.SelectedDevice = new DeviceInformation(1);
                    
                    // Obtener valor actual del estado en memoria
                    byte currentValue = _portStates[port];
                    
                    // Modificar el bit específico
                    byte newValue;
                    if (isChecked)
                    {
                        // Activar bit (OR con máscara)
                        newValue = (byte)(currentValue | (1 << bit));
                    }
                    else
                    {
                        // Desactivar bit (AND con máscara invertida)
                        newValue = (byte)(currentValue & ~(1 << bit));
                    }
                    
                    _actionLogger?.LogUserAction("Digital Port Value Change", 
                        $"Port {port}: {currentValue} (0x{currentValue:X2}) → {newValue} (0x{newValue:X2}), Bit {bit} = {(isChecked ? "1" : "0")}");
                    
                    // Escribir nuevo valor al hardware
                    _actionLogger?.StartTiming();
                    ErrorCode result = _doCtrl.Write(port, newValue);
                    _actionLogger?.StopTiming($"Write Bit P{port}.{bit}={(isChecked ? "1" : "0")}");
                    
                    if (result == ErrorCode.Success)
                    {
                        // Actualizar estado en memoria
                        _portStates[port] = newValue;
                        UpdateOutputStatesDisplay(); // Actualizar visualización
                        
                        _actionLogger?.LogUserAction("Digital Bit Written Successfully", 
                            $"Port {port}, Bit {bit} = {(isChecked ? "1" : "0")}, Port Value = {newValue} (0x{newValue:X2}, Binary: {Convert.ToString(newValue, 2).PadLeft(8, '0')})");
                    }
                    else
                    {
                        _actionLogger?.LogWarning($"Failed to write digital bit: {result}", "DigitalControlPanel");
                        MessageBox.Show($"Error al escribir bit: {result}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        // Revertir checkbox
                        checkbox.IsChecked = !isChecked;
                    }
                }
                else
                {
                    _actionLogger?.LogWarning("No digital devices available for bit toggle", "DigitalControlPanel");
                }
            }
            catch (Exception ex)
            {
                _actionLogger?.LogException("BitToggle", ex);
                MessageBox.Show($"Error al controlar bit: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    /// <summary>
    /// Converter para Bool a Color (para LEDs)
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue 
                    ? Colors.LimeGreen  // Bit en 1
                    : Colors.DimGray;   // Bit en 0
            }
            return Colors.Gray;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

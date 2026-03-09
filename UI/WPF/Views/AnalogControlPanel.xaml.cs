using System;
using System.Windows;
using System.Windows.Controls;
using LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Views
{
    public partial class AnalogControlPanel : UserControl
    {
        public AnalogControlPanel()
        {
            InitializeComponent();
            
            // Track DataContext changes to diagnose binding issues
            DataContextChanged += OnDataContextChanged;
            
            // Log initial state
            Loaded += (s, e) =>
            {
                Console.WriteLine($"[ANALOG PANEL] Loaded. DataContext type: {DataContext?.GetType().Name ?? "NULL"}");
                if (DataContext is AnalogControlViewModel vm)
                {
                    Console.WriteLine($"[ANALOG PANEL] AnalogControlViewModel connected. HashCode: {vm.GetHashCode()}");
                }
                else
                {
                    Console.WriteLine($"[ANALOG PANEL] ERROR: DataContext is NOT AnalogControlViewModel!");
                }
            };
        }
        
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Console.WriteLine($"[ANALOG PANEL] DataContext changed from {e.OldValue?.GetType().Name ?? "NULL"} to {e.NewValue?.GetType().Name ?? "NULL"}");
            
            if (e.NewValue is AnalogControlViewModel vm)
            {
                Console.WriteLine($"[ANALOG PANEL] AnalogControlViewModel set. Instance: {vm.GetHashCode()}");
            }
            else if (e.NewValue != null)
            {
                Console.WriteLine($"[ANALOG PANEL] WARNING: DataContext is {e.NewValue.GetType().FullName}, not AnalogControlViewModel!");
            }
        }
    }
}

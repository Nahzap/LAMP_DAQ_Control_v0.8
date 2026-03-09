using System;
using System.Windows;
using System.Windows.Input;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Views.SignalManager
{
    /// <summary>
    /// Interaction logic for SignalManagerView.xaml
    /// </summary>
    public partial class SignalManagerView : Window
    {
        public SignalManagerView()
        {
            InitializeComponent();
        }

        private void OnSignalMouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Console.WriteLine($"[DRAG] OnSignalMouseDown called. Button: {e.LeftButton}");
            
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                System.Console.WriteLine($"[DRAG] Left button not pressed, ignoring. State: {e.LeftButton}");
                return;
            }

            var border = sender as System.Windows.Controls.Border;
            System.Console.WriteLine($"[DRAG] Border found: {border != null}");
            
            if (border?.Tag is SignalEvent signalEvent)
            {
                System.Console.WriteLine($"[DRAG] SignalEvent found: {signalEvent.Name}, Type: {signalEvent.EventType}, Device: {signalEvent.DeviceType}");
                System.Console.WriteLine($"[DRAG] Starting DragDrop operation with Copy effect...");
                
                try
                {
                    var result = DragDrop.DoDragDrop(border, signalEvent, DragDropEffects.Copy);
                    System.Console.WriteLine($"[DRAG] DragDrop completed. Result: {result}");
                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[DRAG ERROR] Exception during DragDrop: {ex.Message}");
                    System.Console.WriteLine($"[DRAG ERROR] Stack trace: {ex.StackTrace}");
                }
            }
            else
            {
                System.Console.WriteLine($"[DRAG ERROR] SignalEvent not found in Border.Tag. Tag type: {border?.Tag?.GetType().Name ?? "null"}");
            }
        }
    }
}

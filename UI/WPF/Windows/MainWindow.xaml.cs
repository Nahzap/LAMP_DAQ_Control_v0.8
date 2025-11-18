using System;
using System.Windows;
using LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Windows
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Manejar cierre de ventana - LIMPIAR TODO
            Closing += OnWindowClosing;
        }
        
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Liberar todos los recursos del ViewModel
                var viewModel = DataContext as MainViewModel;
                if (viewModel != null)
                {
                    viewModel.Dispose();
                }
                
                // Forzar garbage collection para liberar recursos nativos
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                // Ignorar errores al cerrar - solo log si hay logger
                System.Diagnostics.Debug.WriteLine($"Error al cerrar: {ex.Message}");
            }
        }
    }
}

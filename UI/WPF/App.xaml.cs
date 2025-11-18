using System;
using System.Windows;

namespace LAMP_DAQ_Control_v0_8.UI.WPF
{
    /// <summary>
    /// Lógica de interacción para App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Manejar excepciones no capturadas
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Error no controlado:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                    
                args.Handled = true;
            };
            
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show(
                    $"Error fatal:\n\n{ex?.Message}\n\n{ex?.StackTrace}",
                    "Error Fatal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };
        }
    }
}

using System;
using System.Windows;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;
using LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels;
using LAMP_DAQ_Control_v0_8.UI.WPF.Views;

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
            
            // Conectar ActionLogger a los paneles después de que el DataContext esté configurado
            Loaded += OnWindowLoaded;
        }
        
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            GlobalExceptionLogger.LogInfo("MainWindow.OnWindowLoaded - Starting window initialization...");
            
            try
            {
                var viewModel = DataContext as MainViewModel;
                GlobalExceptionLogger.LogInfo($"MainWindow.OnWindowLoaded - ViewModel type: {viewModel?.GetType().Name ?? "NULL"}");
                
                if (viewModel != null)
                {
                    // Obtener ActionLogger del MainViewModel
                    var actionLogger = viewModel.GetActionLogger();
                    GlobalExceptionLogger.LogInfo($"MainWindow.OnWindowLoaded - ActionLogger obtained: {actionLogger != null}");
                    
                    // Buscar el DigitalControlPanel en el árbol visual y configurar el logger
                    var digitalPanel = FindVisualChild<DigitalControlPanel>(this);
                    GlobalExceptionLogger.LogInfo($"MainWindow.OnWindowLoaded - DigitalControlPanel found: {digitalPanel != null}");
                    
                    if (digitalPanel != null)
                    {
                        digitalPanel.SetActionLogger(actionLogger);
                        actionLogger?.LogUserAction("DigitalControlPanel Logger Connected", "Digital control panel now logging all operations");
                        GlobalExceptionLogger.LogInfo("MainWindow.OnWindowLoaded - DigitalControlPanel logger configured");
                    }
                    
                    // CRITICAL FIX: Buscar AnalogControlPanel y configurar DataContext explícitamente
                    var analogPanel = FindVisualChild<AnalogControlPanel>(this);
                    GlobalExceptionLogger.LogInfo($"MainWindow.OnWindowLoaded - AnalogControlPanel found: {analogPanel != null}");
                    
                    if (analogPanel != null)
                    {
                        analogPanel.DataContext = viewModel.AnalogControl;
                        GlobalExceptionLogger.LogInfo("MainWindow.OnWindowLoaded - AnalogControlPanel DataContext set");
                    }
                    else
                    {
                        GlobalExceptionLogger.LogWarning("MainWindow.OnWindowLoaded - AnalogControlPanel NOT found, subscribing to property changes");
                        // Subscribe to property changes to catch when panel is created dynamically
                        viewModel.PropertyChanged += (s, args) =>
                        {
                            if (args.PropertyName == "IsAnalogDevice" || args.PropertyName == "SelectedDevice")
                            {
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    var panel = FindVisualChild<AnalogControlPanel>(this);
                                    if (panel != null && panel.DataContext == null)
                                    {
                                        panel.DataContext = viewModel.AnalogControl;
                                        GlobalExceptionLogger.LogInfo("MainWindow - AnalogControlPanel DataContext set dynamically after device selection");
                                    }
                                }), System.Windows.Threading.DispatcherPriority.Loaded);
                            }
                        };
                    }
                }
                
                GlobalExceptionLogger.LogInfo("MainWindow.OnWindowLoaded - Initialization completed successfully");
            }
            catch (Exception ex)
            {
                GlobalExceptionLogger.LogCriticalError("MainWindow.OnWindowLoaded - Failed to initialize window", ex);
                System.Diagnostics.Debug.WriteLine($"Error connecting logger: {ex.Message}");
            }
        }
        
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
        
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            GlobalExceptionLogger.LogInfo("MainWindow.OnWindowClosing - Starting cleanup...");
            
            try
            {
                // Liberar todos los recursos del ViewModel
                var viewModel = DataContext as MainViewModel;
                GlobalExceptionLogger.LogInfo($"MainWindow.OnWindowClosing - ViewModel type: {viewModel?.GetType().Name ?? "NULL"}");
                
                if (viewModel != null)
                {
                    GlobalExceptionLogger.LogInfo("MainWindow.OnWindowClosing - Disposing ViewModel...");
                    viewModel.Dispose();
                    GlobalExceptionLogger.LogInfo("MainWindow.OnWindowClosing - ViewModel disposed successfully");
                }
                
                // Forzar recolección de basura para liberar recursos
                GlobalExceptionLogger.LogInfo("MainWindow.OnWindowClosing - Forcing garbage collection...");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GlobalExceptionLogger.LogInfo("MainWindow.OnWindowClosing - Cleanup completed successfully");
                GlobalExceptionLogger.LogInfo("=== APPLICATION SHUTDOWN COMPLETED ===");
            }
            catch (Exception ex)
            {
                GlobalExceptionLogger.LogCriticalError("MainWindow.OnWindowClosing - Error during cleanup", ex);
                System.Diagnostics.Debug.WriteLine($"Error al cerrar: {ex.Message}");
            }
        }
    }
}

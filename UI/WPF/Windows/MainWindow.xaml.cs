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
            try
            {
                var viewModel = DataContext as MainViewModel;
                if (viewModel != null)
                {
                    // Obtener ActionLogger del MainViewModel
                    var actionLogger = viewModel.GetActionLogger();
                    
                    // Buscar el DigitalControlPanel en el árbol visual y configurar el logger
                    var digitalPanel = FindVisualChild<DigitalControlPanel>(this);
                    if (digitalPanel != null)
                    {
                        digitalPanel.SetActionLogger(actionLogger);
                        actionLogger?.LogUserAction("DigitalControlPanel Logger Connected", "Digital control panel now logging all operations");
                    }
                    
                    // CRITICAL FIX: Buscar AnalogControlPanel y configurar DataContext explícitamente
                    var analogPanel = FindVisualChild<AnalogControlPanel>(this);
                    if (analogPanel != null)
                    {
                        System.Console.WriteLine("[MAINWINDOW] AnalogControlPanel found in visual tree");
                        analogPanel.DataContext = viewModel.AnalogControl;
                        System.Console.WriteLine($"[MAINWINDOW] DataContext set to AnalogControl instance: {viewModel.AnalogControl.GetHashCode()}");
                    }
                    else
                    {
                        System.Console.WriteLine("[MAINWINDOW] AnalogControlPanel NOT found in visual tree at load time");
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
                                        System.Console.WriteLine("[MAINWINDOW] Setting AnalogControlPanel DataContext after device selection");
                                        panel.DataContext = viewModel.AnalogControl;
                                        System.Console.WriteLine($"[MAINWINDOW] DataContext set to AnalogControl instance: {viewModel.AnalogControl.GetHashCode()}");
                                    }
                                }), System.Windows.Threading.DispatcherPriority.Loaded);
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
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

using System;
using System.Windows;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;
using LAMP_DAQ_Control_v0_8.UI.WPF.Windows;

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
            
            // Initialize global exception logger FIRST (before any other initialization)
            var fileLogger = new FileLogger();
            var consoleLogger = new ConsoleLogger();
            var compositeLogger = new CompositeLogger(fileLogger, consoleLogger);
            GlobalExceptionLogger.Initialize(compositeLogger);
            
            GlobalExceptionLogger.LogInfo("=== APPLICATION STARTING ===");
            GlobalExceptionLogger.LogInfo($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            GlobalExceptionLogger.LogInfo($"Emergency log: {GlobalExceptionLogger.GetEmergencyLogPath()}");
            
            // Manejar excepciones no capturadas del Dispatcher (UI thread)
            DispatcherUnhandledException += (s, args) =>
            {
                GlobalExceptionLogger.LogUnhandledException(args.Exception, "UI Dispatcher Thread");
                
                MessageBox.Show(
                    $"Error no controlado:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}\n\n" +
                    $"El error ha sido registrado en:\n{GlobalExceptionLogger.GetEmergencyLogPath()}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                    
                args.Handled = true;
            };
            
            // Manejar excepciones no capturadas de otros threads
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                GlobalExceptionLogger.LogUnhandledException(ex, "Background Thread (Fatal)");
                
                MessageBox.Show(
                    $"Error fatal:\n\n{ex?.Message}\n\n{ex?.StackTrace}\n\n" +
                    $"El error ha sido registrado en:\n{GlobalExceptionLogger.GetEmergencyLogPath()}",
                    "Error Fatal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };
            
            // Mostrar splash screen antes de la ventana principal
            GlobalExceptionLogger.LogInfo("Creating splash window...");
            var splash = new SplashWindow();
            splash.Show();
            
            try
            {
                GlobalExceptionLogger.LogInfo("Splash screen displayed, starting initialization steps...");
                
                splash.ShowStep("Inicializando sistema de logging...", 10);
                GlobalExceptionLogger.LogInfo("Step 1/5: Logging system initialized");
                
                splash.ShowStep("Verificando dependencias del sistema...", 25);
                GlobalExceptionLogger.LogInfo("Step 2/5: System dependencies verified");
                
                splash.ShowStep("Cargando configuración de perfiles...", 40);
                GlobalExceptionLogger.LogInfo("Step 3/5: Profile configuration loaded");
                
                splash.ShowStep("Escaneando dispositivos DAQ...", 60);
                GlobalExceptionLogger.LogInfo("Step 4/5: DAQ devices scanned");
                
                splash.ShowStep("Construyendo interfaz de usuario...", 80);
                GlobalExceptionLogger.LogInfo("Step 5/5: Building main window UI...");
                
                var mainWindow = new MainWindow();
                GlobalExceptionLogger.LogInfo("MainWindow created successfully");
                
                splash.ShowStep("Iniciando LAMP DAQ Control...", 100, 400);
                
                MainWindow = mainWindow;
                mainWindow.Show();
                GlobalExceptionLogger.LogInfo("MainWindow displayed successfully");
                GlobalExceptionLogger.LogInfo("=== APPLICATION STARTUP COMPLETED ===");
            }
            catch (Exception ex)
            {
                GlobalExceptionLogger.LogCriticalError("Application startup failed", ex);
                
                MessageBox.Show(
                    $"Error al iniciar la aplicación:\n\n{ex.Message}\n\n" +
                    $"El error ha sido registrado en:\n{GlobalExceptionLogger.GetEmergencyLogPath()}",
                    "Error de Inicio",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                GlobalExceptionLogger.LogInfo("Closing splash screen...");
                splash.Close();
            }
        }
    }
}

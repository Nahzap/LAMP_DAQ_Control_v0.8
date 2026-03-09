using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.UI;

namespace LAMP_DAQ_Control_v0_8
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

        [STAThread] // Requerido para WPF
        static void Main(string[] args)
        {
            // Detectar modo: -console para modo consola, default es WPF
            bool useConsole = args.Length > 0 && args[0].ToLower() == "-console";
            
            if (useConsole)
            {
                RunConsoleMode().Wait();
            }
            else
            {
                RunWPFMode();
            }
        }
        
        static async Task RunConsoleMode()
        {
            Console.Title = "Controlador DAQ Multidevice";
            
            try
            {
                Console.WriteLine("=== Sistema de Control DAQ Multidevice ===");
                Console.WriteLine("Detectando dispositivos disponibles...");
                
                // Crear el controlador DAQ
                using (var controller = new DAQController())
                {
                    // Crear la interfaz de usuario con el controlador
                    var ui = new ConsoleUI(controller);
                    
                    // Ejecutar la interfaz de usuario
                    await ui.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR FATAL: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"DETALLES: {ex.InnerException.Message}");
                }
            }
            finally
            {
                Console.WriteLine("\nPresione cualquier tecla para salir...");
                Console.ReadKey();
            }
        }
        
        static void RunWPFMode()
        {
            // Asignar ventana de consola para mostrar logs
            AllocConsole();
            Console.Title = "LAMP DAQ Control v0.8 - Sistema de Logs";
            Console.WriteLine("========================================");
            Console.WriteLine("LAMP DAQ Control v0.8 - Sistema de Logs");
            Console.WriteLine("========================================");
            Console.WriteLine("Esta ventana muestra todos los logs del sistema en tiempo real.");
            Console.WriteLine("NO CERRAR esta ventana - se cerrará automáticamente al salir de la aplicación.");
            Console.WriteLine("========================================");
            Console.WriteLine();
            
            var app = new UI.WPF.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}

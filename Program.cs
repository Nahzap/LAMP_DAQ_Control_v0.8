using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.UI;

namespace LAMP_DAQ_Control_v0_8
{
    class Program
    {
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
            var app = new UI.WPF.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}

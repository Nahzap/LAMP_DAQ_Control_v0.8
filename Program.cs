using System;
using System.IO;
using System.Threading.Tasks;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.UI;

namespace LAMP_DAQ_Control_v0_8
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Controlador DAQ PCIe-1824";
            
            try
            {
                // Ruta al perfil de configuración
                string profilePath = "PCIe1824_prof_v1.xml";
                
                Console.WriteLine("=== Inicializando Controlador DAQ PCIe-1824 ===");
                Console.WriteLine($"Buscando perfil en: {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, profilePath)}");
                
                using (var controller = new DAQController())
                {
                    // Inicializar el controlador con el perfil
                    controller.Initialize(profilePath);
                    
                    // Mostrar información del dispositivo
                    var deviceInfo = controller.GetDeviceInfo();
                    Console.WriteLine("\n=== INFORMACIÓN DEL DISPOSITIVO ===");
                    Console.WriteLine(deviceInfo.Name);
                    Console.WriteLine($"Canales: {deviceInfo.Channels}");
                    Console.WriteLine("===================================\n");
                    
                    // Iniciar la interfaz de usuario
                    var ui = new ConsoleUI(controller);
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
    }
}

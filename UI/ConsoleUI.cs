using System;
using System.Threading.Tasks;
using LAMP_DAQ_Control_v0._8.Core;

namespace LAMP_DAQ_Control_v0._8.UI
{
    public class ConsoleUI
    {
        private readonly DAQController _controller;
        
        public ConsoleUI(DAQController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }
        
        public async Task Run()
        {
            Console.WriteLine("=== Controlador DAQ PCIe-1824 ===\n");
            _controller.Initialize("PCIe1824_prof_v1.xml");
            await ShowMenu();
        }
        
        private async Task ShowMenu()
        {
            bool exit = false;
            
            while (!exit)
            {
                Console.Clear();
                Console.WriteLine("=== Control de DAQ PCIe-1824 (MODO SENO) ===");
                Console.WriteLine("1. Establecer valor DC en un canal");
                Console.WriteLine("2. Realizar rampa en un canal");
                Console.WriteLine("3. Generar señal senoidal");
                Console.WriteLine("4. Detener generación de señal");
                Console.WriteLine("5. Mostrar información del dispositivo");
                Console.WriteLine("6. Reiniciar todos los canales a 0V");
                Console.WriteLine("7. Salir");
                Console.Write("\nSeleccione una opción: ");
                
                var option = Console.ReadLine();
                
                try
                {
                    switch (option)
                    {
                        case "1":
                            await SetDcValueMenu().ConfigureAwait(false);
                            break;
                        case "2":
                            await RampValueMenu().ConfigureAwait(false);
                            break;
                        case "3":
                            await SignalGenerationMenu().ConfigureAwait(false);
                            break;
                        case "4":
                            _controller.StopSignalGeneration();
                            Console.WriteLine("\nGeneración de señal detenida.");
                            break;
                        case "5":
                            ShowDeviceInfo();
                            break;
                        case "6":
                            _controller.ResetAllChannels();
                            Console.WriteLine("\nTodos los canales han sido reiniciados a 0V.");
                            break;
                        case "7":
                            exit = true;
                            break;
                        default:
                            Console.WriteLine("\nOpción no válida. Por favor, intente de nuevo.");
                            break;
                    }
                    
                    if (!exit)
                    {
                        Console.WriteLine("\nPresione cualquier tecla para continuar...");
                        Console.ReadKey();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"DETALLES: {ex.InnerException.Message}");
                    }
                    Console.WriteLine("Presione cualquier tecla para continuar...");
                    Console.ReadKey();
                }
            }
        }
        
        private Task SetDcValueMenu()
        {
            try
            {
                Console.Write("\nIngrese el número de canal (0-31): ");
                if (!int.TryParse(Console.ReadLine(), out int channel) || channel < 0 || channel > 31)
                {
                    Console.WriteLine("Canal inválido. Debe ser un número entre 0 y 31.");
                    return Task.CompletedTask;
                }
                
                Console.Write("Ingrese el valor en voltios (-10V a 10V): ");
                if (!double.TryParse(Console.ReadLine(), out double value))
                {
                    Console.WriteLine("Valor inválido. Debe ser un número.");
                    return Task.CompletedTask;
                }
                
                _controller.SetChannelValue(channel, value);
                Console.WriteLine($"\nCanal {channel} establecido a {value}V");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al establecer valor DC: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }
        
        private async Task RampValueMenu()
        {
            Console.Write("\nIngrese el número de canal (0-31): ");
            if (!int.TryParse(Console.ReadLine(), out int channel) || channel < 0 || channel > 31)
            {
                Console.WriteLine("Canal inválido. Debe ser un número entre 0 y 31.");
                return;
            }
            
            Console.Write("Ingrese el valor final en voltios (-10V a 10V): ");
            if (!double.TryParse(Console.ReadLine(), out double targetValue))
            {
                Console.WriteLine("Valor inválido. Debe ser un número.");
                return;
            }
            
            Console.Write("Ingrese la duración de la rampa en milisegundos: ");
            if (!int.TryParse(Console.ReadLine(), out int durationMs) || durationMs <= 0)
            {
                Console.WriteLine("Duración inválida. Debe ser un número mayor que cero.");
                return;
            }
            
            Console.WriteLine($"\nIniciando rampa en el canal {channel} hasta {targetValue}V en {durationMs}ms...");
            await _controller.RampChannelValue(channel, targetValue, durationMs);
        }
        
        private async Task SignalGenerationMenu()
        {
            Console.Write("\nIngrese el número de canal (0-31): ");
            if (!int.TryParse(Console.ReadLine(), out int channel) || channel < 0 || channel > 31)
            {
                Console.WriteLine("Canal inválido. Debe ser un número entre 0 y 31.");
                return;
            }
            
            Console.WriteLine("\nGeneración de Señal Senoidal");
            
            Console.Write("Frecuencia (Hz): ");
            if (!double.TryParse(Console.ReadLine(), out double frequency) || frequency <= 0)
            {
                Console.WriteLine("Frecuencia inválida. Debe ser un número mayor que cero.");
                return;
            }
            
            Console.Write("Amplitud (V): ");
            if (!double.TryParse(Console.ReadLine(), out double amplitude) || amplitude <= 0)
            {
                Console.WriteLine("Amplitud inválida. Debe ser un número mayor que cero.");
                return;
            }
            
            Console.Write("Offset (V): ");
            if (!double.TryParse(Console.ReadLine(), out double offset))
            {
                Console.WriteLine("Offset inválido. Debe ser un número.");
                return;
            }
            
            Console.WriteLine($"\nIniciando generación de señal senoidal en el canal {channel}...");
            _controller.StartSignalGeneration(channel, frequency, amplitude, offset);
            Console.WriteLine("Presione cualquier tecla para detener la generación de señal...");
            
            // Esperar a que el usuario presione una tecla para detener
            await Task.Run(() => Console.ReadKey(true));
            _controller.StopSignalGeneration();
            Console.WriteLine("\nGeneración de señal detenida.");
        }
        
        private void ShowDeviceInfo()
        {
            var info = _controller.GetDeviceInfo();
            
            Console.WriteLine("\n=== Información del Dispositivo ===");
            Console.WriteLine($"Nombre: {info.Name}");
            Console.WriteLine($"Canales: {info.Channels}");
            Console.WriteLine("MODO: GENERACIÓN DE SEÑAL SENOIDAL CON LUT");
            
            Console.WriteLine("\nPresione cualquier tecla para continuar...");
            Console.ReadKey();
        }
    }
}

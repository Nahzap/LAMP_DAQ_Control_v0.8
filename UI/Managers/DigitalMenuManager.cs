using System;
using System.Threading.Tasks;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.UI.Interfaces;
using LAMP_DAQ_Control_v0_8.UI.Models;

namespace LAMP_DAQ_Control_v0_8.UI.Managers
{
    /// <summary>
    /// Gestor de menús específicos para dispositivos digitales
    /// </summary>
    public class DigitalMenuManager : IDeviceMenuHandler
    {
        private readonly DAQController _controller;
        private readonly IConsoleService _consoleService;
        
        public DigitalMenuManager(DAQController controller, IConsoleService consoleService)
        {
            _controller = controller;
            _consoleService = consoleService;
        }
        
        public async Task HandleDeviceMenu(DAQDevice device)
        {
            bool exit = false;
            
            while (!exit)
            {
                int option = _consoleService.ShowMenu(
                    $"Dispositivo Digital: {device.Name}",
                    new[]
                    {
                        "Leer puerto digital",
                        "Escribir puerto digital",
                        "Leer bit digital",
                        "Escribir bit digital",
                        "Mostrar información del dispositivo",
                        "Salir"
                    }
                );
                
                switch (option)
                {
                    case 1: ReadDigitalPort(); break;
                    case 2: WriteDigitalPort(); break;
                    case 3: ReadDigitalBit(); break;
                    case 4: WriteDigitalBit(); break;
                    case 5: ShowDeviceInfo(device); break;
                    case 6: exit = true; break;
                }
            }
        }
        
        // Métodos específicos para dispositivos digitales
        private void ReadDigitalPort()
        {
            try
            {
                _consoleService.ShowMessage("\n=== Leer Puerto Digital ===");
                
                int port = _consoleService.GetIntInput("Ingrese el número de puerto (0-3): ", 0, 3);
                
                byte value = _controller.ReadDigitalPort(port);
                _consoleService.ShowMessage($"Valor leído del puerto {port}: {value} (0x{value:X})");
                
                // Mostrar bits individuales
                _consoleService.ShowMessage("\nEstado de bits:");
                for (int i = 0; i < 8; i++)
                {
                    bool bitValue = ((value >> i) & 1) == 1;
                    _consoleService.ShowMessage($"Bit {i}: {(bitValue ? "1" : "0")}");
                }
                
                _consoleService.ShowMessage("\nPresione cualquier tecla para continuar...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al leer puerto digital: {ex.Message}");
            }
        }
        
        private void WriteDigitalPort()
        {
            try
            {
                _consoleService.ShowMessage("\n=== Escribir Puerto Digital ===");
                
                int port = _consoleService.GetIntInput("Ingrese el número de puerto (0-3): ", 0, 3);
                byte value = (byte)_consoleService.GetIntInput("Ingrese el valor a escribir (0-255): ", 0, 255);
                
                _controller.WriteDigitalPort(port, value);
                _consoleService.ShowMessage($"Valor {value} (0x{value:X}) escrito en puerto {port}");
                
                _consoleService.ShowMessage("\nPresione cualquier tecla para continuar...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al escribir puerto digital: {ex.Message}");
            }
        }
        
        private void ReadDigitalBit()
        {
            try
            {
                _consoleService.ShowMessage("\n=== Leer Bit Digital ===");
                
                int port = _consoleService.GetIntInput("Ingrese el número de puerto (0-3): ", 0, 3);
                int bit = _consoleService.GetIntInput("Ingrese el número de bit (0-7): ", 0, 7);
                
                bool value = _controller.ReadDigitalBit(port, bit);
                _consoleService.ShowMessage($"Valor del bit {bit} en puerto {port}: {(value ? "1" : "0")}");
                
                _consoleService.ShowMessage("\nPresione cualquier tecla para continuar...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al leer bit digital: {ex.Message}");
            }
        }
        
        private void WriteDigitalBit()
        {
            try
            {
                _consoleService.ShowMessage("\n=== Escribir Bit Digital ===");
                
                int port = _consoleService.GetIntInput("Ingrese el número de puerto (0-3): ", 0, 3);
                int bit = _consoleService.GetIntInput("Ingrese el número de bit (0-7): ", 0, 7);
                int bitValue = _consoleService.GetIntInput("Ingrese el valor (0 o 1): ", 0, 1);
                
                _controller.WriteDigitalBit(port, bit, bitValue == 1);
                _consoleService.ShowMessage($"Bit {bit} en puerto {port} establecido a {bitValue}");
                
                _consoleService.ShowMessage("\nPresione cualquier tecla para continuar...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al escribir bit digital: {ex.Message}");
            }
        }
        
        private void ShowDeviceInfo(DAQDevice device)
        {
            try
            {
                var deviceInfo = _controller.GetDeviceInfo();
                
                _consoleService.ShowMessage("\n=== Información del Dispositivo ===");
                _consoleService.ShowMessage($"Nombre: {deviceInfo.Name}");
                _consoleService.ShowMessage($"Canales: {deviceInfo.Channels}");
                _consoleService.ShowMessage($"Tipo: {deviceInfo.DeviceType}");
                _consoleService.ShowMessage($"Info adicional: {deviceInfo.AdditionalInfo ?? "No disponible"}");
                
                _consoleService.ShowMessage("\nPresione cualquier tecla para continuar...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al mostrar información del dispositivo: {ex.Message}");
            }
        }
    }
}

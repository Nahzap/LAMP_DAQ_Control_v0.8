using System;
using System.Threading.Tasks;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.UI.Interfaces;
using LAMP_DAQ_Control_v0_8.UI.Models;

namespace LAMP_DAQ_Control_v0_8.UI.Managers
{
    /// <summary>
    /// Gestor de menús específicos para dispositivos analógicos
    /// </summary>
    public class AnalogMenuManager : IDeviceMenuHandler
    {
        private readonly DAQController _controller;
        private readonly IConsoleService _consoleService;
        
        public AnalogMenuManager(DAQController controller, IConsoleService consoleService)
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
                    $"Dispositivo Analógico: {device.Name}",
                    new[]
                    {
                        "Establecer valor DC",
                        "Generar rampa",
                        "Generación de señal",
                        "Mostrar información del dispositivo",
                        "Salir"
                    }
                );
                
                switch (option)
                {
                    case 1: await SetDcValue(); break;
                    case 2: await GenerateRamp(); break;
                    case 3: await GenerateSignal(); break;
                    case 4: ShowDeviceInfo(device); break;
                    case 5: exit = true; break;
                }
            }
        }
        
        // Métodos específicos para dispositivos analógicos
        private async Task SetDcValue()
        {
            try
            {
                _consoleService.ShowMessage("\n=== Establecer Valor DC ===");
                
                int channel = _consoleService.GetIntInput("Ingrese el número de canal (0-7): ", 0, 7);
                double voltage = _consoleService.GetDoubleInput("Ingrese el voltaje (0V a 10V): ", 0, 10);
                
                _controller.WriteVoltage(channel, voltage);
                _consoleService.ShowMessage($"Valor DC establecido en canal {channel}: {voltage}V");
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al establecer valor DC: {ex.Message}");
            }
        }
        
        private async Task GenerateRamp()
        {
            try
            {
                _consoleService.ShowMessage("\n=== Generar Rampa ===");
                
                int channel = _consoleService.GetIntInput("Ingrese el número de canal (0-7): ", 0, 7);
                double targetVoltage = _consoleService.GetDoubleInput("Ingrese el voltaje objetivo (0V a 10V): ", 0, 10);
                int durationMs = _consoleService.GetIntInput("Ingrese la duración de la rampa (ms): ", 100, 10000);
                
                _controller.RampChannelValue(channel, targetVoltage, durationMs);
                _consoleService.ShowMessage("Rampa generada correctamente");
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al generar rampa: {ex.Message}");
            }
        }
        
        private async Task GenerateSignal()
        {
            try
            {
                _consoleService.ShowMessage("\n=== Generación de Señal ===");
                
                int channel = _consoleService.GetIntInput("Ingrese el número de canal (0-7): ", 0, 7);
                
                double frequency = _consoleService.GetDoubleInput("Ingrese la frecuencia (0.1 a 100Hz): ", 0.1, 100);
                double amplitude = _consoleService.GetDoubleInput("Ingrese la amplitud (0 a 10V): ", 0, 10);
                double offset = _consoleService.GetDoubleInput("Ingrese el offset (0V a 10V): ", 0, 10);
                
                _controller.StartSignalGeneration(channel, frequency, amplitude, offset);
                _consoleService.ShowMessage("Señal generada correctamente");
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al generar señal: {ex.Message}");
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

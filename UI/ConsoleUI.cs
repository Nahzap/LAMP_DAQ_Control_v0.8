using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.UI.Interfaces;
using LAMP_DAQ_Control_v0_8.UI.Managers;
using LAMP_DAQ_Control_v0_8.UI.Services;
using LAMP_DAQ_Control_v0_8.UI.Models;
using LAMP_DAQ_Control_v0_8.UI.Exceptions;

namespace LAMP_DAQ_Control_v0_8.UI
{
    /// <summary>
    /// Interfaz de usuario de consola para el control de dispositivos DAQ
    /// </summary>
    public class ConsoleUI
    {
        #region Campos y Constructor
        
        private readonly DAQController _controller;
        private readonly DeviceDetectionService _detectionService;
        private readonly MenuManager _menuManager;
        private readonly IConsoleService _consoleService;
        
        public ConsoleUI(DAQController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _consoleService = new ConsoleService();
            _detectionService = new DeviceDetectionService(_consoleService);
            _menuManager = new MenuManager(_controller, _consoleService);
        }
        
        #endregion
        
        #region Ejecución Principal
        
        public async Task Run()
        {
            try
            {
                _consoleService.ShowMessage("=== Sistema de Control DAQ Multidevice ===\n");
                
                // Detectar tarjetas DAQ disponibles
                var devices = _detectionService.DetectDAQDevices();
                
                if (devices.Count == 0)
                {
                    _consoleService.ShowError("No se encontraron tarjetas DAQ conectadas.");
                    _consoleService.ShowMessage("Presione cualquier tecla para salir...");
                    Console.ReadKey();
                    return;
                }
                
                // Mostrar menú principal y gestionar selección de dispositivo
                var selectedDevice = await SelectDevice(devices);
                if (selectedDevice == null)
                {
                    _consoleService.ShowMessage("No se seleccionó ningún dispositivo. Saliendo...");
                    return;
                }
                
                // Inicializar controlador con el dispositivo seleccionado
                bool initialized = await InitializeController(selectedDevice);
                if (!initialized)
                {
                    _consoleService.ShowError("No se pudo inicializar el controlador DAQ.");
                    _consoleService.ShowMessage("Presione cualquier tecla para salir...");
                    Console.ReadKey();
                    return;
                }
                
                // Mostrar menú de operaciones según el tipo de dispositivo (ya inicializado)
                await _menuManager.ShowDeviceMenu(selectedDevice);
            }
            catch (UIException ex)
            {
                _consoleService.ShowError($"Error de UI: {ex.Message}");
                _consoleService.ShowMessage("Presione cualquier tecla para continuar...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error fatal: {ex.Message}");
                _consoleService.ShowMessage("Presione cualquier tecla para salir...");
                Console.ReadKey();
            }
        }
        
        #endregion
        
        #region Métodos Auxiliares
        
        private async Task<DAQDevice> SelectDevice(List<DAQDevice> devices)
        {
            _consoleService.ShowMessage("\n=== Selección de Dispositivo DAQ ===\n");
            
            for (int i = 0; i < devices.Count; i++)
            {
                string deviceType = devices[i].DeviceType == DeviceType.Analog ? "Analógico" : "Digital";
                _consoleService.ShowMessage($"{i + 1}. {devices[i].Name} (Tipo: {deviceType})");
            }
            
            _consoleService.ShowMessage($"{devices.Count + 1}. Salir");
            
            int selection = _consoleService.GetIntInput("\nSeleccione un dispositivo: ", 1, devices.Count + 1);
            
            if (selection == devices.Count + 1)
            {
                return null; // Usuario seleccionó salir
            }
            
            return devices[selection - 1];
        }
        
        private async Task<bool> InitializeController(DAQDevice device)
        {
            try
            {
                _consoleService.ShowMessage($"\nInicializando dispositivo {device.Name}...");
                
                // Inicializar el controlador con el dispositivo seleccionado
                _controller.Initialize(device.ConfigFile, device.DeviceNumber);
                
                _consoleService.ShowMessage("Dispositivo inicializado correctamente.");
                return true;
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al inicializar el dispositivo: {ex.Message}");
                return false;
            }
        }
        
        #endregion
    }
}

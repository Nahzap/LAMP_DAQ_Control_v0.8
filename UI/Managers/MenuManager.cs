using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.UI.Interfaces;
using LAMP_DAQ_Control_v0_8.UI.Models;

namespace LAMP_DAQ_Control_v0_8.UI.Managers
{
    /// <summary>
    /// Gestor principal de menús de la aplicación
    /// </summary>
    public class MenuManager
    {
        private readonly IConsoleService _consoleService;
        private readonly Dictionary<DeviceType, IDeviceMenuHandler> _deviceMenuHandlers;
        private readonly DAQController _controller;
        
        public MenuManager(DAQController controller, IConsoleService consoleService)
        {
            _controller = controller;
            _consoleService = consoleService;
            
            _deviceMenuHandlers = new Dictionary<DeviceType, IDeviceMenuHandler>
            {
                { DeviceType.Analog, new AnalogMenuManager(controller, consoleService) },
                { DeviceType.Digital, new DigitalMenuManager(controller, consoleService) }
            };
        }
        
        public async Task ShowMainMenu(List<DAQDevice> devices)
        {
            var selectedDevice = SelectDevice(devices);
            if (selectedDevice == null) return;
            
            try
            {
                _consoleService.ShowMessage($"\nInicializando {selectedDevice.Name}...");
                _controller.Initialize(selectedDevice.ConfigFile, selectedDevice.DeviceNumber);
                
                if (_deviceMenuHandlers.TryGetValue(selectedDevice.DeviceType, out var handler))
                {
                    await handler.HandleDeviceMenu(selectedDevice);
                }
                else
                {
                    _consoleService.ShowError($"Tipo de dispositivo no soportado: {selectedDevice.DeviceType}");
                }
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al inicializar dispositivo: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Muestra el menú del dispositivo ya inicializado (sin volver a pedir selección)
        /// </summary>
        public async Task ShowDeviceMenu(DAQDevice device)
        {
            try
            {
                if (_deviceMenuHandlers.TryGetValue(device.DeviceType, out var handler))
                {
                    await handler.HandleDeviceMenu(device);
                }
                else
                {
                    _consoleService.ShowError($"Tipo de dispositivo no soportado: {device.DeviceType}");
                }
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"Error al mostrar menú del dispositivo: {ex.Message}");
            }
        }
        
        private DAQDevice SelectDevice(List<DAQDevice> devices)
        {
            if (devices == null || !devices.Any())
            {
                _consoleService.ShowError("No hay dispositivos disponibles para seleccionar.");
                return null;
            }

            _consoleService.ShowMessage("\n=== Dispositivos DAQ Detectados ===");
            for (int i = 0; i < devices.Count; i++)
            {
                _consoleService.ShowMessage($"{i + 1}. {devices[i].Name} (Tipo: {devices[i].DeviceType})");
            }

            int selection = _consoleService.GetIntInput("\nSeleccione un dispositivo: ", 1, devices.Count);
            return devices[selection - 1];
        }
    }
}

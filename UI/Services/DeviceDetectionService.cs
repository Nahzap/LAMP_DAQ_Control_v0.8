using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.UI.Interfaces;
using LAMP_DAQ_Control_v0_8.UI.Models;

namespace LAMP_DAQ_Control_v0_8.UI.Services
{
    /// <summary>
    /// Servicio para la detección de dispositivos DAQ
    /// </summary>
    public class DeviceDetectionService
    {
        private readonly IConsoleService _consoleService;
        
        public DeviceDetectionService(IConsoleService consoleService)
        {
            _consoleService = consoleService;
        }
        
        public List<DAQDevice> DetectDAQDevices()
        {
            _consoleService.ShowMessage("Detectando dispositivos DAQ disponibles...");
            
            var devices = new List<DAQDevice>();
            
            _consoleService.ShowMessage("\n=== LISTADO DE TODOS LOS DISPOSITIVOS ADVANTECH DISPONIBLES ===\n");
            
            // Listar dispositivos disponibles por tipo de controlador
            ListAvailableDevices();
            
            // Listar perfiles disponibles
            string profilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core", "DAQ", "Profiles");
            var profileFiles = Directory.GetFiles(profilesPath, "*.xml").Select(Path.GetFileNameWithoutExtension).ToList();
            
            _consoleService.ShowMessage($"\nPerfiles disponibles: {profileFiles.Count}");
            foreach (var profile in profileFiles)
            {
                _consoleService.ShowMessage($"- {profile}");
            }
            
            // Detectar dispositivos en cada posición
            for (int i = 0; i < 8; i++)
            {
                _consoleService.ShowMessage($"\n=== Analizando posición {i} ===");
                var device = DetectDAQDeviceByBoardId(i);
                if (device != null)
                {
                    devices.Add(device);
                }
            }
            
            // Mostrar resumen de detección
            _consoleService.ShowMessage("\n=== Resumen de detección de dispositivos ===");
            _consoleService.ShowMessage($"Dispositivos analógicos detectados: {devices.Count(d => d.DeviceType == DeviceType.Analog)}");
            _consoleService.ShowMessage($"Dispositivos digitales detectados: {devices.Count(d => d.DeviceType == DeviceType.Digital)}");
            _consoleService.ShowMessage($"Total de dispositivos DAQ: {devices.Count}");
            
            return devices;
        }
        
        private void ListAvailableDevices()
        {
            // Listar dispositivos AI
            _consoleService.ShowMessage("Dispositivos disponibles para InstantAiCtrl:");
            var aiCtrl = new InstantAiCtrl();
            for (int i = 0; i < aiCtrl.SupportedDevices.Count; i++)
            {
                _consoleService.ShowMessage($"  [{i}] {aiCtrl.SupportedDevices[i]}");
            }
            
            // Listar dispositivos AO
            _consoleService.ShowMessage("\nDispositivos disponibles para InstantAoCtrl:");
            var aoCtrl = new InstantAoCtrl();
            for (int i = 0; i < aoCtrl.SupportedDevices.Count; i++)
            {
                _consoleService.ShowMessage($"  [{i}] {aoCtrl.SupportedDevices[i]}");
            }
            
            // Listar dispositivos DI
            _consoleService.ShowMessage("\nDispositivos disponibles para InstantDiCtrl:");
            var diCtrl = new InstantDiCtrl();
            for (int i = 0; i < diCtrl.SupportedDevices.Count; i++)
            {
                _consoleService.ShowMessage($"  [{i}] {diCtrl.SupportedDevices[i]}");
            }
            
            // Listar dispositivos DO
            _consoleService.ShowMessage("\nDispositivos disponibles para InstantDoCtrl:");
            var doCtrl = new InstantDoCtrl();
            for (int i = 0; i < doCtrl.SupportedDevices.Count; i++)
            {
                _consoleService.ShowMessage($"  [{i}] {doCtrl.SupportedDevices[i]}");
            }
        }
        
        private DAQDevice DetectDAQDeviceByBoardId(int deviceNumber)
        {
            _consoleService.ShowMessage($"- Intentando detectar dispositivo en posición {deviceNumber}...");
            
            // Primero intentamos detectar el dispositivo directamente
            string detectedDeviceName = "";
            int detectedBoardId = -1;
            DeviceType detectedType = DeviceType.Unknown;
            
            // Intentar detectar dispositivo digital
            if (TryDetectDigitalDevice(deviceNumber, out detectedDeviceName, out detectedBoardId))
            {
                detectedType = DeviceType.Digital;
                _consoleService.ShowMessage($"  Dispositivo digital detectado: {detectedDeviceName} (Board ID: {detectedBoardId})");
            }
            // Si no se detectó como digital, intentar como analógico
            else if (TryDetectAnalogDevice(deviceNumber, out detectedDeviceName, out detectedBoardId))
            {
                detectedType = DeviceType.Analog;
                _consoleService.ShowMessage($"  Dispositivo analógico detectado: {detectedDeviceName} (Board ID: {detectedBoardId})");
            }
            else
            {
                _consoleService.ShowMessage($"  No se detectó ningún dispositivo en posición {deviceNumber}");
                return null;
            }
            
            // Si se detectó un dispositivo, buscar un perfil compatible
            string profilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core", "DAQ", "Profiles");
            var profileFiles = Directory.GetFiles(profilesPath, "*.xml");
            
            foreach (var profileFile in profileFiles)
            {
                try
                {
                    // Cargar perfil XML
                    var xdoc = XDocument.Load(profileFile);
                    var profileName = Path.GetFileNameWithoutExtension(profileFile);
                    var deviceNameInProfile = xdoc.Root?.Element("DeviceName")?.Value;
                    var productIdElement = xdoc.Root?.Element("ProductID");
                    
                    if (productIdElement != null && int.TryParse(productIdElement.Value, out int productId))
                    {
                        _consoleService.ShowMessage($"  Verificando compatibilidad con perfil: {profileName} - {deviceNameInProfile} (ProductID: {productId})");
                        
                        // Intentar detectar dispositivo según su tipo
                        if (DetermineDeviceType(productId) == DeviceType.Digital)
                        {
                            // Intentar detectar dispositivo digital
                            if (TryDetectDigitalDevice(deviceNumber, productId, out string detectedDeviceName, out int boardId))
                            {
                                _consoleService.ShowMessage($"  Dispositivo digital detectado con InstantDiCtrl: {detectedDeviceName} (Board ID: {boardId})");
                                _consoleService.ShowMessage($"  Tipo de dispositivo detectado: Digital");
                                _consoleService.ShowMessage($"  Perfil asignado: {profileName}");
                                
                                return new DAQDevice
                                {
                                    Name = detectedDeviceName,
                                    DeviceNumber = deviceNumber,
                                    ConfigFile = profileFile,
                                    DeviceType = DeviceType.Digital,
                                    BoardId = boardId
                                };
                            }
                        }
                        else if (DetermineDeviceType(productId) == DeviceType.Analog)
                        {
                            // Intentar detectar dispositivo analógico
                            if (TryDetectAnalogDevice(deviceNumber, productId, out string detectedDeviceName, out int boardId))
                            {
                                _consoleService.ShowMessage($"  Dispositivo analógico detectado con InstantAoCtrl: {detectedDeviceName} (Board ID: {boardId})");
                                _consoleService.ShowMessage($"  Tipo de dispositivo detectado: Analógico");
                                _consoleService.ShowMessage($"  Perfil asignado: {profileName}");
                                
                                return new DAQDevice
                                {
                                    Name = detectedDeviceName,
                                    DeviceNumber = deviceNumber,
                                    ConfigFile = profileFile,
                                    DeviceType = DeviceType.Analog,
                                    BoardId = boardId
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _consoleService.ShowError($"  Error al procesar perfil {profileFile}: {ex.Message}");
                }
            }
            
            _consoleService.ShowMessage($"  No se detectó ningún dispositivo en posición {deviceNumber}");
            return null;
        }
        
        private bool TryDetectDigitalDevice(int deviceNumber, int productId, out string deviceName, out int boardId)
        {
            deviceName = string.Empty;
            boardId = -1;
            
            try
            {
                // Intentar con InstantDiCtrl
                using (var diCtrl = new InstantDiCtrl())
                {
                    diCtrl.SelectedDevice = new DeviceInformation(deviceNumber);
                    
                    if (diCtrl.Initialized)
                    {
                        // Verificar si el dispositivo coincide con el ProductID esperado
                        if (diCtrl.Device.DeviceNumber == deviceNumber)
                        {
                            deviceName = diCtrl.Device.Description;
                            boardId = diCtrl.Device.BoardId;
                            
                            // Verificar si el dispositivo es compatible con el perfil
                            if (boardId == productId || deviceName.Contains(productId.ToString()))
                            {
                                // Verificar propiedades específicas para PCI-1735U
                                if (diCtrl.Features.PortCount == 4 && diCtrl.Features.ChannelCountMax == 32)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                
                // Si no se detectó con DI, intentar con DO
                using (var doCtrl = new InstantDoCtrl())
                {
                    doCtrl.SelectedDevice = new DeviceInformation(deviceNumber);
                    
                    if (doCtrl.Initialized)
                    {
                        // Verificar si el dispositivo coincide con el ProductID esperado
                        if (doCtrl.Device.DeviceNumber == deviceNumber)
                        {
                            deviceName = doCtrl.Device.Description;
                            boardId = doCtrl.Device.BoardId;
                            
                            // Verificar si el dispositivo es compatible con el perfil
                            if (boardId == productId || deviceName.Contains(productId.ToString()))
                            {
                                // Verificar propiedades específicas para PCI-1735U
                                if (doCtrl.Features.PortCount == 4 && doCtrl.Features.ChannelCountMax == 32)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"  Error al detectar dispositivo digital: {ex.Message}");
            }
            
            return false;
        }
        
        private bool TryDetectAnalogDevice(int deviceNumber, int productId, out string deviceName, out int boardId)
        {
            deviceName = string.Empty;
            boardId = -1;
            
            try
            {
                using (var aoCtrl = new InstantAoCtrl())
                {
                    aoCtrl.SelectedDevice = new DeviceInformation(deviceNumber);
                    
                    if (aoCtrl.Initialized)
                    {
                        // Verificar si el dispositivo coincide con el ProductID esperado
                        if (aoCtrl.Device.DeviceNumber == deviceNumber)
                        {
                            deviceName = aoCtrl.Device.Description;
                            boardId = aoCtrl.Device.BoardId;
                            
                            // Verificar si el dispositivo es compatible con el perfil
                            if (boardId == productId || deviceName.Contains(productId.ToString()))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"  Error al detectar dispositivo analógico: {ex.Message}");
            }
            
            return false;
        }
        
        private DeviceType DetermineDeviceType(int productId)
        {
            // Determinar el tipo de dispositivo según el ProductID
            // PCI-1735U (ProductID: 187) es un dispositivo digital
            // PCIe-1824 (ProductID: 2110) es un dispositivo analógico
            switch (productId)
            {
                case 187:  // PCI-1735U
                    return DeviceType.Digital;
                case 2110: // PCIe-1824
                    return DeviceType.Analog;
                default:
                    // Por defecto, asumir analógico
                    return DeviceType.Analog;
            }
        }
    }
}

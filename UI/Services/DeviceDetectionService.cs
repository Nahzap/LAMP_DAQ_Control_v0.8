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
            var detectedBoardIds = new HashSet<int>();
            
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
            
            // Detectar dispositivos digitales (DI y DO)
            _consoleService.ShowMessage("\n=== Detectando dispositivos digitales ===");
            DetectDigitalDevices(devices, detectedBoardIds);
            
            // Detectar dispositivos analógicos (AO)
            _consoleService.ShowMessage("\n=== Detectando dispositivos analógicos ===");
            DetectAnalogDevices(devices, detectedBoardIds);
            
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
            if (TryDetectDigitalDevice(deviceNumber, 0, out detectedDeviceName, out detectedBoardId))
            {
                detectedType = DeviceType.Digital;
                _consoleService.ShowMessage($"  Dispositivo digital detectado: {detectedDeviceName} (Board ID: {detectedBoardId})");
            }
            // Si no se detectó como digital, intentar como analógico
            else if (TryDetectAnalogDevice(deviceNumber, 0, out detectedDeviceName, out detectedBoardId))
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
                            if (TryDetectDigitalDevice(deviceNumber, productId, out string digitalDeviceName, out int digitalBoardId))
                            {
                                _consoleService.ShowMessage($"  Dispositivo digital detectado con InstantDiCtrl: {digitalDeviceName} (Board ID: {digitalBoardId})");
                                _consoleService.ShowMessage($"  Tipo de dispositivo detectado: Digital");
                                _consoleService.ShowMessage($"  Perfil asignado: {profileName}");
                                
                                return new DAQDevice
                                {
                                    Name = digitalDeviceName,
                                    DeviceNumber = deviceNumber,
                                    ConfigFile = profileFile,
                                    DeviceType = DeviceType.Digital,
                                    BoardId = digitalBoardId
                                };
                            }
                        }
                        else if (DetermineDeviceType(productId) == DeviceType.Analog)
                        {
                            // Intentar detectar dispositivo analógico
                            if (TryDetectAnalogDevice(deviceNumber, productId, out string analogDeviceName, out int analogBoardId))
                            {
                                _consoleService.ShowMessage($"  Dispositivo analógico detectado con InstantAoCtrl: {analogDeviceName} (Board ID: {analogBoardId})");
                                _consoleService.ShowMessage($"  Tipo de dispositivo detectado: Analógico");
                                _consoleService.ShowMessage($"  Perfil asignado: {profileName}");
                                
                                return new DAQDevice
                                {
                                    Name = analogDeviceName,
                                    DeviceNumber = deviceNumber,
                                    ConfigFile = profileFile,
                                    DeviceType = DeviceType.Analog,
                                    BoardId = analogBoardId
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
        
        private void DetectDigitalDevices(List<DAQDevice> devices, HashSet<int> detectedBoardIds)
        {
            try
            {
                // Detectar dispositivos DI
                var diCtrl = new InstantDiCtrl();
                for (int i = 0; i < diCtrl.SupportedDevices.Count; i++)
                {
                    try
                    {
                        string deviceInfo = diCtrl.SupportedDevices[i].ToString();
                        int boardId = ExtractBoardId(deviceInfo);
                        if (!detectedBoardIds.Contains(boardId) && !string.IsNullOrEmpty(deviceInfo) && boardId > 0)
                        {
                            _consoleService.ShowMessage($"  Dispositivo DI detectado: {deviceInfo}");
                            
                            var device = new DAQDevice
                            {
                                Name = deviceInfo,
                                DeviceNumber = boardId,
                                DeviceType = DeviceType.Digital,
                                BoardId = boardId,
                                ConfigFile = FindProfileForDevice(deviceInfo)
                            };
                            
                            devices.Add(device);
                            detectedBoardIds.Add(boardId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _consoleService.ShowError($"  Error al procesar dispositivo DI en posición {i}: {ex.Message}");
                    }
                }
                
                // Detectar dispositivos DO
                var doCtrl = new InstantDoCtrl();
                for (int i = 0; i < doCtrl.SupportedDevices.Count; i++)
                {
                    try
                    {
                        string deviceInfo = doCtrl.SupportedDevices[i].ToString();
                        int boardId = ExtractBoardId(deviceInfo);
                        if (!detectedBoardIds.Contains(boardId) && !string.IsNullOrEmpty(deviceInfo) && boardId > 0)
                        {
                            _consoleService.ShowMessage($"  Dispositivo DO detectado: {deviceInfo}");
                            
                            var device = new DAQDevice
                            {
                                Name = deviceInfo,
                                DeviceNumber = boardId,
                                DeviceType = DeviceType.Digital,
                                BoardId = boardId,
                                ConfigFile = FindProfileForDevice(deviceInfo)
                            };
                            
                            devices.Add(device);
                            detectedBoardIds.Add(boardId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _consoleService.ShowError($"  Error al procesar dispositivo DO en posición {i}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"  Error general al detectar dispositivos digitales: {ex.Message}");
            }
        }
        
        private void DetectAnalogDevices(List<DAQDevice> devices, HashSet<int> detectedBoardIds)
        {
            try
            {
                // Detectar dispositivos AO
                var aoCtrl = new InstantAoCtrl();
                for (int i = 0; i < aoCtrl.SupportedDevices.Count; i++)
                {
                    try
                    {
                        string deviceInfo = aoCtrl.SupportedDevices[i].ToString();
                        int boardId = ExtractBoardId(deviceInfo);
                        if (!detectedBoardIds.Contains(boardId) && !string.IsNullOrEmpty(deviceInfo) && boardId > 0)
                        {
                            _consoleService.ShowMessage($"  Dispositivo AO detectado: {deviceInfo}");
                            
                            var device = new DAQDevice
                            {
                                Name = deviceInfo,
                                DeviceNumber = boardId,
                                DeviceType = DeviceType.Analog,
                                BoardId = boardId,
                                ConfigFile = FindProfileForDevice(deviceInfo)
                            };
                            
                            devices.Add(device);
                            detectedBoardIds.Add(boardId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _consoleService.ShowError($"  Error al procesar dispositivo AO en posición {i}: {ex.Message}");
                    }
                }
                
                // Detectar dispositivos AI
                var aiCtrl = new InstantAiCtrl();
                for (int i = 0; i < aiCtrl.SupportedDevices.Count; i++)
                {
                    try
                    {
                        string deviceInfo = aiCtrl.SupportedDevices[i].ToString();
                        int boardId = ExtractBoardId(deviceInfo);
                        if (!detectedBoardIds.Contains(boardId) && !string.IsNullOrEmpty(deviceInfo) && boardId > 0)
                        {
                            _consoleService.ShowMessage($"  Dispositivo AI detectado: {deviceInfo}");
                            
                            var device = new DAQDevice
                            {
                                Name = deviceInfo,
                                DeviceNumber = i,
                                DeviceType = DeviceType.Analog,
                                BoardId = boardId,
                                ConfigFile = FindProfileForDevice(deviceInfo)
                            };
                            
                            devices.Add(device);
                            detectedBoardIds.Add(boardId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _consoleService.ShowError($"  Error al procesar dispositivo AI en posición {i}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"  Error general al detectar dispositivos analógicos: {ex.Message}");
            }
        }
        
        private int ExtractBoardId(string deviceDescription)
        {
            try
            {
                // Formato esperado: "PCIE-1824,BID#12" o "PCI-1735U,BID#3"
                if (deviceDescription.Contains("BID#"))
                {
                    var parts = deviceDescription.Split(new[] { "BID#" }, StringSplitOptions.None);
                    if (parts.Length > 1 && int.TryParse(parts[1], out int boardId))
                    {
                        return boardId;
                    }
                }
            }
            catch
            {
                // Continuar con valor por defecto
            }
            
            return -1;
        }
        
        private string FindProfileForDevice(string deviceDescription)
        {
            try
            {
                string profilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Core", "DAQ", "Profiles");
                
                if (!Directory.Exists(profilesPath))
                {
                    _consoleService.ShowError($"  Directorio de perfiles no encontrado: {profilesPath}");
                    return string.Empty;
                }
                
                var profileFiles = Directory.GetFiles(profilesPath, "*.xml");
                
                // Extraer el modelo del dispositivo (ej: PCI-1735U, PCIE-1824)
                string deviceModel = deviceDescription.Split(',')[0].Trim();
                _consoleService.ShowMessage($"  Buscando perfil para: {deviceModel}");
                
                foreach (var profileFile in profileFiles)
                {
                    try
                    {
                        // Comparar el nombre del archivo del perfil con el modelo del dispositivo
                        string profileName = Path.GetFileNameWithoutExtension(profileFile);
                        
                        // Normalizar nombres para comparación (quitar guiones y convertir a mayúsculas)
                        string normalizedProfile = profileName.Replace("-", "").Replace("_", "").ToUpperInvariant();
                        string normalizedDevice = deviceModel.Replace("-", "").Replace("_", "").ToUpperInvariant();
                        
                        _consoleService.ShowMessage($"    Comparando: '{normalizedDevice}' con '{normalizedProfile}'");
                        
                        // Verificar si el perfil contiene el modelo del dispositivo
                        if (normalizedProfile.Contains(normalizedDevice))
                        {
                            _consoleService.ShowMessage($"  ✓ Perfil encontrado: {profileFile}");
                            return profileFile;
                        }
                    }
                    catch (Exception ex)
                    {
                        _consoleService.ShowError($"    Error al procesar perfil {profileFile}: {ex.Message}");
                    }
                }
                
                _consoleService.ShowMessage($"  ✗ No se encontró perfil para {deviceModel}");
            }
            catch (Exception ex)
            {
                _consoleService.ShowError($"  Error al buscar perfil para dispositivo: {ex.Message}");
            }
            
            return string.Empty;
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

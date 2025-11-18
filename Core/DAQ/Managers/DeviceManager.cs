using System;
using System.Collections.Generic;
using System.Linq;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Exceptions;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Managers
{
    /// <summary>
    /// Manages DAQ device initialization and operations
    /// </summary>
    public class DeviceManager : IDeviceManager, IDisposable
    {
        // Controladores para diferentes tipos de dispositivos
        private InstantAoCtrl _analogDevice;
        private InstantDiCtrl _digitalInputDevice;
        private InstantDoCtrl _digitalOutputDevice;
        
        private readonly ILogger _logger;
        private bool _deviceInitialized;
        private bool _disposed;
        private string _deviceModel;
        private int _deviceNumber;
        private DeviceType _deviceType;
        private const int MAX_DEVICES_TO_CHECK = 8; // Aumentado para buscar más dispositivos
        
        public DeviceManager(ILogger logger = null)
        {
            _logger = logger ?? new ConsoleLogger();
            _deviceType = DeviceType.Unknown;
            
            try
            {
                // Inicializamos los controladores pero no los conectamos a ningún dispositivo aún
                _analogDevice = new InstantAoCtrl();
                _digitalInputDevice = new InstantDiCtrl();
                _digitalOutputDevice = new InstantDoCtrl();
                
                _logger.Info("Device manager created successfully");
            }
            catch (Exception ex)
            {
                const string errorMsg = "Error creating device manager";
                _logger.Error(errorMsg, ex);
                throw new DAQInitializationException(errorMsg, ex);
            }
        }

        public bool IsInitialized => _deviceInitialized && !_disposed;

        // El conteo de canales depende del tipo de dispositivo
        public int ChannelCount
        {
            get
            {
                switch (_deviceType)
                {
                    case DeviceType.Analog:
                        return _analogDevice?.Channels?.Length ?? 0;
                    case DeviceType.Digital:
                        // Para dispositivos digitales, devolvemos el número de puertos * 8 (bits por puerto)
                        if (_digitalInputDevice != null && _digitalInputDevice.Initialized)
                            return _digitalInputDevice.PortCount * 8;
                        if (_digitalOutputDevice != null && _digitalOutputDevice.Initialized)
                            return _digitalOutputDevice.PortCount * 8;
                        return 0;
                    default:
                        return 0;
                }
            }
        }

        public string DeviceModel => _deviceModel ?? "Unknown";
        
        public DeviceType CurrentDeviceType => _deviceType;

        // Mantenemos esta propiedad por compatibilidad con la interfaz IDeviceManager
        public InstantAoCtrl Device => _analogDevice;

        public void InitializeDevice(int deviceNumber, string profileName = null)
        {
            EnsureNotDisposed();
            _deviceNumber = deviceNumber;

            try
            {
                if (_deviceInitialized)
                {
                    _logger.Info("Device is already initialized");
                    return;
                }
                
                _logger.Info($"Detectando tipo de dispositivo para ID {deviceNumber}, perfil: {profileName ?? "(ninguno)"}...");
                
                // Determinar el tipo de dispositivo basado en el nombre del perfil
                bool isDigitalProfile = !string.IsNullOrEmpty(profileName) && 
                                       (profileName.Contains("PCI1735") || profileName.Contains("1735"));
                
                if (isDigitalProfile)
                {
                    _logger.Info("Perfil digital detectado, intentando inicializar como PCI-1735U...");
                    
                    // Intentar inicializar como dispositivo digital primero
                    if (TryInitializeDigitalDevice(deviceNumber))
                    {
                        _deviceType = DeviceType.Digital;
                        _logger.Info($"Dispositivo digital inicializado: {_deviceModel}");
                        return;
                    }
                    
                    _logger.Warn("No se pudo inicializar como dispositivo digital a pesar de tener perfil digital.");
                }
                
                // Si no es un perfil digital o falló la inicialización digital, intentamos como analógico
                if (TryInitializeAnalogDevice(deviceNumber))
                {
                    _deviceType = DeviceType.Analog;
                    _logger.Info($"Dispositivo analógico inicializado: {_deviceModel}");
                    return;
                }
                
                // Si aún no se ha inicializado y no es un perfil digital específico, intentamos como digital
                if (!isDigitalProfile && TryInitializeDigitalDevice(deviceNumber))
                {
                    _deviceType = DeviceType.Digital;
                    _logger.Info($"Dispositivo digital inicializado: {_deviceModel}");
                    return;
                }
                
                // Si llegamos aquí, no se pudo inicializar ningún tipo de dispositivo
                throw new DAQInitializationException($"No se pudo inicializar el dispositivo {deviceNumber} como analógico ni digital");
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to initialize DAQ device {deviceNumber}";
                _logger.Error(errorMsg, ex);
                throw new DAQInitializationException(errorMsg, ex);
            }
        }
        
        private bool TryInitializeAnalogDevice(int deviceNumber)
        {
            _logger.Info($"Intentando inicializar como dispositivo analógico (PCIe-1824)...");
            try
            {
                // Buscar el índice del dispositivo con el Board ID especificado
                var deviceCount = _analogDevice.SupportedDevices.Count;
                _logger.Info($"Dispositivos analógicos disponibles: {deviceCount}");
                
                int actualDeviceNumber = -1;
                for (int i = 0; i < deviceCount; i++)
                {
                    var deviceInfo = _analogDevice.SupportedDevices[i];
                    _logger.Info($"Dispositivo disponible {i}: {deviceInfo.Description}, ID: {deviceInfo.DeviceNumber}");
                    
                    if (deviceInfo.DeviceNumber == deviceNumber || 
                        deviceInfo.Description.Contains($"BID#{deviceNumber}"))
                    {
                        actualDeviceNumber = deviceInfo.DeviceNumber;
                        _logger.Info($"✓ Encontrado dispositivo analógico con Board ID {deviceNumber}, DeviceNumber={actualDeviceNumber}");
                        break;
                    }
                }
                
                if (actualDeviceNumber == -1)
                {
                    _logger.Info($"No se encontró dispositivo analógico con Board ID {deviceNumber}");
                    return false;
                }
                
                _analogDevice.SelectedDevice = new DeviceInformation(actualDeviceNumber);
                _deviceModel = _analogDevice.SelectedDevice.Description;
                
                // Verificar si es un dispositivo analógico (PCIe-1824)
                if (_deviceModel.Contains("PCIe-1824") || _deviceModel.Contains("1824"))
                {
                    // Test communication with a write operation
                    _analogDevice.Write(0, 0.0);
                    _deviceInitialized = true;
                    return true;
                }
                
                _logger.Info("No es un dispositivo analógico compatible");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Info($"No se pudo inicializar como dispositivo analógico: {ex.Message}");
                return false;
            }
        }
        
        private bool TryInitializeDigitalDevice(int deviceNumber)
        {
            _logger.Info($"Intentando inicializar como dispositivo digital (PCI-1735U)...");
            
            // Primero intentamos con entrada digital
            if (TryInitializeDigitalInputDevice(deviceNumber))
            {
                return true;
            }
            
            // Si falla, intentamos con salida digital
            return TryInitializeDigitalOutputDevice(deviceNumber);
        }
        
        private bool TryInitializeDigitalInputDevice(int deviceNumber)
        {
            _logger.Info($"Intentando inicializar como dispositivo de entrada digital con Board ID: {deviceNumber}...");
            try
            {
                // Buscar todos los dispositivos disponibles
                var deviceCount = _digitalInputDevice.SupportedDevices.Count;
                _logger.Info($"Dispositivos de entrada digital disponibles: {deviceCount}");
                
                // Intentar inicializar directamente con el Board ID proporcionado
                try
                {
                    // Crear un DeviceInformation usando el Board ID
                    _logger.Info($"Intentando inicializar directamente con Board ID: {deviceNumber}");
                    
                    // Primero verificamos si el dispositivo existe en la lista de dispositivos soportados
                    bool deviceFound = false;
                    string deviceDesc = "";
                    int actualDeviceNumber = -1;
                    
                    for (int i = 0; i < deviceCount; i++)
                    {
                        var deviceInfo = _digitalInputDevice.SupportedDevices[i];
                        _logger.Info($"Dispositivo disponible {i}: {deviceInfo.Description}, ID: {deviceInfo.DeviceNumber}");
                        
                        if (deviceInfo.DeviceNumber == deviceNumber || 
                            deviceInfo.Description.Contains($"BID#{deviceNumber}"))
                        {
                            deviceFound = true;
                            deviceDesc = deviceInfo.Description;
                            actualDeviceNumber = deviceInfo.DeviceNumber;
                            _logger.Info($"✓ Encontrado dispositivo con Board ID {deviceNumber}: {deviceDesc}, DeviceNumber={actualDeviceNumber}");
                            break;
                        }
                    }
                    
                    if (!deviceFound)
                    {
                        _logger.Info($"No se encontró dispositivo de entrada digital con Board ID {deviceNumber}");
                        return false;
                    }
                    
                    // Inicializar el dispositivo con el DeviceNumber correcto
                    _digitalInputDevice.SelectedDevice = new DeviceInformation(actualDeviceNumber);
                    _deviceModel = _digitalInputDevice.SelectedDevice.Description;
                    
                    // Verificar propiedades específicas de la PCI-1735U
                    _logger.Info($"Dispositivo inicializado: {_deviceModel}");
                    _logger.Info($"PortCount: {_digitalInputDevice.PortCount}");
                    
                    // La PCI-1735U debe tener 4 puertos
                    if (_digitalInputDevice.PortCount == 4)
                    {
                        // Test communication with a read operation
                        byte data = 0;
                        _digitalInputDevice.Read(0, out data);
                        _logger.Info($"✓ Lectura exitosa del puerto 0: {data}");
                        _deviceInitialized = true;
                        return true;
                    }
                    else
                    {
                        _logger.Info($"El dispositivo no tiene 4 puertos (tiene {_digitalInputDevice.PortCount})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Info($"Error al inicializar dispositivo con Board ID {deviceNumber}: {ex.Message}");
                }
                
                _logger.Info("No es un dispositivo de entrada digital compatible");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Info($"No se pudo inicializar como dispositivo de entrada digital: {ex.Message}");
                return false;
            }
        }
        
        private bool TryInitializeDigitalOutputDevice(int deviceNumber)
        {
            _logger.Info($"Intentando inicializar como dispositivo de salida digital con Board ID: {deviceNumber}...");
            try
            {
                // Buscar todos los dispositivos disponibles
                var deviceCount = _digitalOutputDevice.SupportedDevices.Count;
                _logger.Info($"Dispositivos de salida digital disponibles: {deviceCount}");
                
                // Intentar inicializar directamente con el Board ID proporcionado
                try
                {
                    // Crear un DeviceInformation usando el Board ID
                    _logger.Info($"Intentando inicializar directamente con Board ID: {deviceNumber}");
                    
                    // Primero verificamos si el dispositivo existe en la lista de dispositivos soportados
                    bool deviceFound = false;
                    string deviceDesc = "";
                    int actualDeviceNumber = -1;
                    
                    for (int i = 0; i < deviceCount; i++)
                    {
                        var deviceInfo = _digitalOutputDevice.SupportedDevices[i];
                        _logger.Info($"Dispositivo disponible {i}: {deviceInfo.Description}, ID: {deviceInfo.DeviceNumber}");
                        
                        if (deviceInfo.DeviceNumber == deviceNumber || 
                            deviceInfo.Description.Contains($"BID#{deviceNumber}"))
                        {
                            deviceFound = true;
                            deviceDesc = deviceInfo.Description;
                            actualDeviceNumber = deviceInfo.DeviceNumber;
                            _logger.Info($"✓ Encontrado dispositivo con Board ID {deviceNumber}: {deviceDesc}, DeviceNumber={actualDeviceNumber}");
                            break;
                        }
                    }
                    
                    if (!deviceFound)
                    {
                        _logger.Info($"No se encontró dispositivo de salida digital con Board ID {deviceNumber}");
                        return false;
                    }
                    
                    // Inicializar el dispositivo con el DeviceNumber correcto
                    _digitalOutputDevice.SelectedDevice = new DeviceInformation(actualDeviceNumber);
                    _deviceModel = _digitalOutputDevice.SelectedDevice.Description;
                    
                    // Verificar propiedades específicas de la PCI-1735U
                    _logger.Info($"Dispositivo inicializado: {_deviceModel}");
                    _logger.Info($"PortCount: {_digitalOutputDevice.PortCount}");
                    
                    // La PCI-1735U debe tener 4 puertos
                    if (_digitalOutputDevice.PortCount == 4)
                    {
                        // Test communication with a write operation
                        byte data = 0;
                        _digitalOutputDevice.Write(0, data);
                        _logger.Info($"✓ Escritura exitosa al puerto 0: {data}");
                        _deviceInitialized = true;
                        return true;
                    }
                    else
                    {
                        _logger.Info($"El dispositivo no tiene 4 puertos (tiene {_digitalOutputDevice.PortCount})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Info($"Error al inicializar dispositivo con Board ID {deviceNumber}: {ex.Message}");
                }
                
                _logger.Info("No es un dispositivo de salida digital compatible");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Info($"No se pudo inicializar como dispositivo de salida digital: {ex.Message}");
                return false;
            }
        }

        public void WriteVoltage(int channel, double value)
        {
            EnsureInitialized();
            ValidateChannelNumber(channel);

            try
            {
                // Solo los dispositivos analógicos soportan escritura de voltaje
                if (_deviceType == DeviceType.Analog)
                {
                    _analogDevice.Write(channel, value);
                    _logger.Debug($"Channel {channel} updated to {value}V");
                }
                else
                {
                    throw new InvalidOperationException("WriteVoltage solo es válido para dispositivos analógicos");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error writing to channel {channel}", ex);
                throw new DAQOperationException($"Error writing to channel {channel}", ex);
            }
        }
        
        // Métodos nuevos para dispositivos digitales
        
        /// <summary>
        /// Escribe un valor digital (0 o 1) en un bit específico
        /// </summary>
        /// <param name="port">Número de puerto</param>
        /// <param name="bit">Número de bit (0-7)</param>
        /// <param name="value">Valor (true=1, false=0)</param>
        public void WriteDigitalBit(int port, int bit, bool value)
        {
            EnsureInitialized();
            
            try
            {
                if (_deviceType == DeviceType.Digital && _digitalOutputDevice != null)
                {
                    byte data = value ? (byte)1 : (byte)0;
                    _digitalOutputDevice.WriteBit(port, bit, data);
                    _logger.Debug($"Digital port {port}, bit {bit} updated to {value}");
                }
                else
                {
                    throw new InvalidOperationException("WriteDigitalBit solo es válido para dispositivos digitales");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error writing to digital port {port}, bit {bit}", ex);
                throw new DAQOperationException($"Error writing to digital port {port}, bit {bit}", ex);
            }
        }
        
        /// <summary>
        /// Lee un valor digital (0 o 1) de un bit específico
        /// </summary>
        /// <param name="port">Número de puerto</param>
        /// <param name="bit">Número de bit (0-7)</param>
        /// <returns>Valor leído (true=1, false=0)</returns>
        public bool ReadDigitalBit(int port, int bit)
        {
            EnsureInitialized();
            
            try
            {
                if (_deviceType == DeviceType.Digital && _digitalInputDevice != null)
                {
                    byte data = 0;
                    _digitalInputDevice.ReadBit(port, bit, out data);
                    bool value = data != 0;
                    _logger.Debug($"Digital port {port}, bit {bit} read: {value}");
                    return value;
                }
                else
                {
                    throw new InvalidOperationException("ReadDigitalBit solo es válido para dispositivos digitales");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error reading from digital port {port}, bit {bit}", ex);
                throw new DAQOperationException($"Error reading from digital port {port}, bit {bit}", ex);
            }
        }
        
        /// <summary>
        /// Escribe un byte completo (8 bits) en un puerto digital
        /// </summary>
        /// <param name="port">Número de puerto</param>
        /// <param name="value">Valor a escribir (0-255)</param>
        public void WriteDigitalPort(int port, byte value)
        {
            EnsureInitialized();
            
            try
            {
                if (_deviceType == DeviceType.Digital && _digitalOutputDevice != null)
                {
                    _digitalOutputDevice.Write(port, value);
                    _logger.Debug($"Digital port {port} updated to {value}");
                }
                else
                {
                    throw new InvalidOperationException("WriteDigitalPort solo es válido para dispositivos digitales");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error writing to digital port {port}", ex);
                throw new DAQOperationException($"Error writing to digital port {port}", ex);
            }
        }
        
        /// <summary>
        /// Lee un byte completo (8 bits) de un puerto digital
        /// </summary>
        /// <param name="port">Número de puerto</param>
        /// <returns>Valor leído (0-255)</returns>
        public byte ReadDigitalPort(int port)
        {
            EnsureInitialized();
            
            try
            {
                if (_deviceType == DeviceType.Digital && _digitalInputDevice != null)
                {
                    byte data = 0;
                    _digitalInputDevice.Read(port, out data);
                    _logger.Debug($"Digital port {port} read: {data}");
                    return data;
                }
                else
                {
                    throw new InvalidOperationException("ReadDigitalPort solo es válido para dispositivos digitales");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error reading from digital port {port}", ex);
                throw new DAQOperationException($"Error reading from digital port {port}", ex);
            }
        }

        public IList<DeviceInfo> DetectDevices()
        {
            var devices = new List<DeviceInfo>();
            
            // Detectar dispositivos analógicos (PCIe-1824)
            _logger.Info("Buscando dispositivos analógicos (PCIe-1824)...");
            for (int i = 0; i < MAX_DEVICES_TO_CHECK; i++)
            {
                using (var daq = new InstantAoCtrl())
                {
                    try
                    {
                        daq.SelectedDevice = new DeviceInformation(i);
                        string description = daq.SelectedDevice.Description;
                        
                        if (description.Contains("PCIe-1824") || description.Contains("1824"))
                        {
                            int channelCount = daq.Channels != null ? daq.Channels.Length : 0;
                            var deviceInfo = new DeviceInfo($"PCIe-1824 (ID: {i})", channelCount)
                            {
                                AdditionalInfo = "Dispositivo analógico",
                                DeviceType = DeviceType.Analog
                            };
                            devices.Add(deviceInfo);
                            _logger.Info($"Detectado dispositivo analógico {i}: {description}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug($"No se encontró dispositivo analógico en posición {i}: {ex.Message}");
                    }
                }
            }
            
            // Detectar dispositivos digitales (PCI-1735U)
            _logger.Info("Buscando dispositivos digitales (PCI-1735U)...");
            for (int i = 0; i < MAX_DEVICES_TO_CHECK; i++)
            {
                // Primero intentamos con InstantDiCtrl
                try
                {
                    using (var diCtrl = new InstantDiCtrl())
                    {
                        diCtrl.SelectedDevice = new DeviceInformation(i);
                        string description = diCtrl.SelectedDevice.Description;
                        
                        if ((description.Contains("PCI-1735") || description.Contains("1735")) && 
                            diCtrl.PortCount == 4)
                        {
                            // Verificar que no hayamos agregado ya este dispositivo
                            if (!devices.Any(d => d.Name.Contains($"ID: {i}")))
                            {
                                var deviceInfo = new DeviceInfo($"PCI-1735U (ID: {i})", diCtrl.PortCount * 8)
                                {
                                    AdditionalInfo = "Dispositivo digital",
                                    DeviceType = DeviceType.Digital
                                };
                                devices.Add(deviceInfo);
                                _logger.Info($"Detectado dispositivo digital {i}: {description}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug($"No se encontró dispositivo digital (DI) en posición {i}: {ex.Message}");
                }
                
                // Si no se detectó, intentamos con InstantDoCtrl
                if (!devices.Any(d => d.Name.Contains($"ID: {i}") && d.DeviceType == DeviceType.Digital))
                {
                    try
                    {
                        using (var doCtrl = new InstantDoCtrl())
                        {
                            doCtrl.SelectedDevice = new DeviceInformation(i);
                            string description = doCtrl.SelectedDevice.Description;
                            
                            if ((description.Contains("PCI-1735") || description.Contains("1735")) && 
                                doCtrl.PortCount == 4)
                            {
                                // Verificar que no hayamos agregado ya este dispositivo
                                if (!devices.Any(d => d.Name.Contains($"ID: {i}")))
                                {
                                    var deviceInfo = new DeviceInfo($"PCI-1735U (ID: {i})", doCtrl.PortCount * 8)
                                    {
                                        AdditionalInfo = "Dispositivo digital",
                                        DeviceType = DeviceType.Digital
                                    };
                                    devices.Add(deviceInfo);
                                    _logger.Info($"Detectado dispositivo digital {i}: {description}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug($"No se encontró dispositivo digital (DO) en posición {i}: {ex.Message}");
                    }
                }
            }
            
            _logger.Info($"Detectados {devices.Count} dispositivos en total");
            _logger.Info($"Dispositivos analógicos: {devices.Count(d => d.DeviceType == DeviceType.Analog)}");
            _logger.Info($"Dispositivos digitales: {devices.Count(d => d.DeviceType == DeviceType.Digital)}");
            return devices;
        }

        public DeviceInfo GetDeviceInfo()
        {
            EnsureInitialized();
            
            try
            {
                string deviceName;
                int channelCount;
                string additionalInfo;
                
                switch (_deviceType)
                {
                    case DeviceType.Analog:
                        // Para dispositivos analógicos, usamos la información del InstantAoCtrl
                        deviceName = $"{_analogDevice.SelectedDevice.Description} (ID: {_deviceNumber})";
                        channelCount = _analogDevice.Channels?.Length ?? 0;
                        additionalInfo = $"Dispositivo analógico, Status: {(_deviceInitialized ? "Initialized" : "Not Initialized")}";
                        break;
                        
                    case DeviceType.Digital:
                        // Para dispositivos digitales, usamos la información específica de PCI-1735U
                        deviceName = $"PCI-1735U (ID: {_deviceNumber})";
                        
                        // Para dispositivos digitales, calculamos el número de canales como puertos * 8 bits
                        int portCount = 0;
                        if (_digitalInputDevice != null && _digitalInputDevice.Initialized)
                        {
                            portCount = _digitalInputDevice.PortCount;
                            _logger.Debug($"DI PortCount: {portCount}");
                        }
                        else if (_digitalOutputDevice != null && _digitalOutputDevice.Initialized)
                        {
                            portCount = _digitalOutputDevice.PortCount;
                            _logger.Debug($"DO PortCount: {portCount}");
                        }
                        else
                        {
                            // Si no hay información disponible, usamos el valor estándar para PCI-1735U
                            portCount = 4;
                            _logger.Debug("Usando valor estándar de 4 puertos para PCI-1735U");
                        }
                        
                        channelCount = portCount * 8;
                        additionalInfo = $"Dispositivo digital, Status: {(_deviceInitialized ? "Initialized" : "Not Initialized")}";
                        break;
                        
                    default:
                        deviceName = "Unknown Device";
                        channelCount = 0;
                        additionalInfo = "Dispositivo no inicializado";
                        break;
                }
                
                var info = new DeviceInfo(deviceName, channelCount)
                {
                    AdditionalInfo = additionalInfo,
                    DeviceType = _deviceType
                };
                
                _logger.Info($"Device Info - {info}");
                return info;
            }
            catch (Exception ex)
            {
                _logger.Error("Error getting device information", ex);
                throw new DAQOperationException("Failed to retrieve device information", ex);
            }
        }

        public void ConfigureChannels(ValueRange range)
        {
            EnsureInitialized();
            
            // Solo los dispositivos analógicos soportan configuración de canales con ValueRange
            if (_deviceType != DeviceType.Analog)
            {
                throw new InvalidOperationException("ConfigureChannels solo es válido para dispositivos analógicos");
            }
            
            if (_analogDevice.Channels == null || _analogDevice.Channels.Length == 0)
                throw new DAQInitializationException("No channels found on the device");

            double initialValue = (range == ValueRange.mA_4To20) ? 4.0 : 0.0;
            
            _logger.Info($"Configuring {_analogDevice.Channels.Length} channels with range: {range}");

            for (int i = 0; i < _analogDevice.Channels.Length; i++)
            {
                try
                {
                    _analogDevice.Channels[i].ValueRange = range;
                    _analogDevice.Write(i, initialValue);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Error configuring channel {i}: {ex.Message}");
                }
            }
            
            _logger.Info("Channel configuration completed");
        }
        
        /// <summary>
        /// Configura los puertos digitales (solo para dispositivos digitales)
        /// </summary>
        public void ConfigureDigitalPorts()
        {
            EnsureInitialized();
            
            if (_deviceType != DeviceType.Digital)
            {
                throw new InvalidOperationException("ConfigureDigitalPorts solo es válido para dispositivos digitales");
            }
            
            _logger.Info("Configurando puertos digitales...");
            
            try
            {
                // Si tenemos un dispositivo de salida digital, inicializamos todos los puertos a 0
                if (_deviceType == DeviceType.Digital && _digitalOutputDevice != null)
                {
                    try
                    {
                        for (int port = 0; port < _digitalOutputDevice.PortCount; port++)
                        {
                            byte zero = 0;
                            _digitalOutputDevice.Write(port, zero);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error resetting digital outputs", ex);
                    }
                }
                
                _logger.Info("Configuración de puertos digitales completada");
            }
            catch (Exception ex)
            {
                _logger.Error("Error configurando puertos digitales", ex);
                throw new DAQOperationException("Error configurando puertos digitales", ex);
            }
        }

        private void ValidateChannelNumber(int channel)
        {
            if (channel < 0 || (ChannelCount > 0 && channel >= ChannelCount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(channel), 
                    $"Channel must be between 0 and {ChannelCount - 1}");
            }
        }

        private void EnsureInitialized()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DeviceManager));
                
            if (!_deviceInitialized)
                throw new InvalidOperationException("Device is not initialized");
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DeviceManager));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Liberar recursos según el tipo de dispositivo
                    if (_deviceInitialized)
                    {
                        switch (_deviceType)
                        {
                            case DeviceType.Analog:
                                // Reset all analog outputs to 0V
                                if (_analogDevice != null)
                                {
                                    for (int i = 0; i < _analogDevice.ChannelCount; i++)
                                    {
                                        try { _analogDevice.Write(i, 0.0); }
                                        catch { /* Ignore errors during cleanup */ }
                                    }
                                }
                                break;
                                
                            case DeviceType.Digital:
                                // Reset all digital outputs to 0
                                if (_deviceType == DeviceType.Digital && _digitalOutputDevice != null)
                                {
                                    try
                                    {
                                        for (int port = 0; port < _digitalOutputDevice.PortCount; port++)
                                        {
                                            byte zero = 0;
                                            _digitalOutputDevice.Write(port, zero);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.Error("Error resetting digital outputs", ex);
                                    }
                                }
                                break;
                        }
                    }
                    
                    // Dispose all device controllers
                    _analogDevice?.Dispose();
                    _digitalInputDevice?.Dispose();
                    _digitalOutputDevice?.Dispose();
                    
                    _logger.Info("Device resources released");
                }
                catch (Exception ex)
                {
                    _logger.Error("Error releasing device resources", ex);
                }
                
                _disposed = true;
                _deviceInitialized = false;
            }
        }

        ~DeviceManager()
        {
            Dispose(false);
        }
    }
}

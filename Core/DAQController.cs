using System;
using System.IO;
using Automation.BDaq;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using LAMP_DAQ_Control_v0._8.Core;

namespace LAMP_DAQ_Control_v0._8
{
    public class DAQController : IDisposable
    {
        private readonly InstantAoCtrl _device;
        private bool _deviceInitialized = false;
        private bool _disposed = false;
        private readonly SignalGenerator _signalGenerator;
        
        public DAQController()
        {
            try
            {
                Console.WriteLine("Inicializando controlador DAQ...");
                _device = new InstantAoCtrl();
                _signalGenerator = new SignalGenerator(_device);
                
                // 1. Primero intentar cargar el perfil
                string defaultProfilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCIe1824_prof_v1.xml");
                
                if (File.Exists(defaultProfilePath))
                {
                    try
                    {
                        Console.WriteLine($"Cargando perfil: {defaultProfilePath}");
                        _device.LoadProfile(defaultProfilePath);
                        Console.WriteLine("Perfil cargado exitosamente");
                        
                        // Verificar si el dispositivo se configuró correctamente
                        try
                        {
                            // Verificar si el dispositivo tiene una descripción válida
                            if (!string.IsNullOrEmpty(_device.SelectedDevice.Description))
                            {
                                Console.WriteLine($"Dispositivo: {_device.SelectedDevice.Description} (ID: {_device.SelectedDevice.DeviceNumber})");
                                _deviceInitialized = true;
                            }
                            else
                            {
                                Console.WriteLine("ADVERTENCIA: El dispositivo no tiene una descripción válida");
                                _deviceInitialized = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error al verificar el dispositivo: {ex.Message}");
                            _deviceInitialized = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al cargar el perfil: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Detalles: {ex.InnerException.Message}");
                        }
                        _deviceInitialized = false;
                    }
                }
                else
                {
                    Console.WriteLine($"No se encontró el archivo de perfil en: {defaultProfilePath}");
                    _deviceInitialized = false;
                }
                
                // 2. Si no se pudo inicializar con el perfil, intentar configuración manual
                if (!_deviceInitialized)
                {
                    Console.WriteLine("\nIntentando configuración manual...");
                    
                    try
                    {
                        // Configurar manualmente para PCIe-1824
                        _device.SelectedDevice = new DeviceInformation(0); // Usar el primer dispositivo
                        Console.WriteLine($"Dispositivo configurado manualmente: {_device.SelectedDevice.Description} (ID: {_device.SelectedDevice.DeviceNumber})");
                        
                        // Configurar canales manualmente
                        for (int i = 0; i < 32; i++) // 32 canales para PCIe-1824
                        {
                            try
                            {
                                _device.Channels[i].ValueRange = ValueRange.V_Neg10To10;
                                _device.Write(i, 0.0); // Inicializar a 0V
                                Console.WriteLine($"Canal {i} configurado a rango ±10V e inicializado a 0V");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error al configurar canal {i}: {ex.Message}");
                            }
                        }
                        
                        _deviceInitialized = true;
                        Console.WriteLine("Configuración manual completada exitosamente");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error en configuración manual: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Detalles: {ex.InnerException.Message}");
                        }
                        _deviceInitialized = false;
                    }
                }
                
                Console.WriteLine($"\nEstado de inicialización: {(_deviceInitialized ? "CORRECTO" : "FALLIDO")}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el controlador DAQ: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Detalles: {ex.InnerException.Message}");
                }
                throw;
            }
        }
        
        private bool TryAutoSelectDevice()
        {
            try
            {
                Console.WriteLine("Inicializando dispositivo PCIe-1824...");
                
                // 1. Intentar cargar el perfil directamente
                string defaultProfilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCIe1824_prof_v1.xml");
                if (File.Exists(defaultProfilePath))
                {
                    try
                    {
                        _device.LoadProfile(defaultProfilePath);
                        Console.WriteLine($"Perfil cargado correctamente: {Path.GetFileName(defaultProfilePath)}");
                        _deviceInitialized = true;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al cargar el perfil: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Detalles: {ex.InnerException.Message}");
                        }
                        // Continuar con configuración manual si falla la carga del perfil
                    }
                }
                
                // 2. Configuración manual si no hay perfil o falla la carga
                Console.WriteLine("Configurando canales manualmente...");
                
                try
                {
                    // Configurar el número de canales manualmente (32 para PCIe-1824)
                    int numChannels = 32;
                    
                    // Configurar cada canal
                    for (int i = 0; i < numChannels; i++)
                    {
                        try
                        {
                            _device.Channels[i].ValueRange = ValueRange.V_Neg10To10;
                            _device.Write(i, 0.0); // Inicializar a 0V
                            Console.WriteLine($"Canal {i} configurado a rango ±10V e inicializado a 0V");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error al configurar canal {i}: {ex.Message}");
                            if (ex.InnerException != null)
                            {
                                Console.WriteLine($"Detalles: {ex.InnerException.Message}");
                            }
                            // Continuar con el siguiente canal aunque falle uno
                        }
                    }
                    
                    _deviceInitialized = true;
                    Console.WriteLine($"Configuración manual completada para {numChannels} canales");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en la configuración manual: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Detalles: {ex.InnerException.Message}");
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al inicializar el dispositivo: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Detalles: {ex.InnerException.Message}");
                }
                return false;
            }
        }
        
        /// <summary>
        /// Inicializa el controlador DAQ con la configuración especificada
        /// </summary>
        /// <param name="profilePath">Ruta opcional al archivo de perfil de configuración</param>
        /// <exception cref="InvalidOperationException">Cuando no se puede inicializar el dispositivo</exception>
        public void Initialize(string profilePath = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DAQController));

            try
            {
                // 1. Verificar si ya está inicializado
                if (_deviceInitialized)
                {
                    LogInfo("El dispositivo ya está inicializado");
                    return;
                }

                // 2. Intentar cargar perfil desde la ruta proporcionada
                if (!string.IsNullOrEmpty(profilePath) && File.Exists(profilePath))
                {
                    try
                    {
                        _device.LoadProfile(profilePath);
                        LogInfo($"Perfil cargado correctamente: {Path.GetFileName(profilePath)}");
                        _deviceInitialized = true;
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"Error al cargar el perfil {profilePath}: {ex.Message}");
                    }
                }

                // 3. Si no se proporcionó perfil o falló, buscar en la ruta por defecto
                string defaultProfilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCIe1824_prof_v1.xml");
                if (File.Exists(defaultProfilePath))
                {
                    try
                    {
                        _device.LoadProfile(defaultProfilePath);
                        LogInfo($"Perfil por defecto cargado: {defaultProfilePath}");
                        _deviceInitialized = true;
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"Error al cargar el perfil por defecto: {ex.Message}");
                    }
                }

                // 4. Configuración manual si no hay perfil
                LogInfo("Iniciando configuración manual...");
                
                // Verificar si el dispositivo tiene canales
                if (_device.Channels == null || _device.Channels.Length == 0)
                {
                    throw new InvalidOperationException("No se encontraron canales en el dispositivo");
                }

                // Configurar cada canal manualmente
                for (int i = 0; i < _device.Channels.Length; i++)
                {
                    try
                    {
                        _device.Channels[i].ValueRange = ValueRange.V_Neg10To10;
                        _device.Write(i, 0.0); // Inicializar a 0V
                        LogInfo($"Canal {i} configurado a rango ±10V e inicializado a 0V");
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"Error al configurar canal {i}: {ex.Message}");
                    }
                }
                
                _deviceInitialized = true;
                LogInfo("Configuración manual completada correctamente");
                
            }
            catch (Exception ex) when (IsRecoverableException(ex))
            {
                _deviceInitialized = false;
                string errorMsg = "Error durante la inicialización del dispositivo DAQ";
                LogError(errorMsg, ex);
                throw new InvalidOperationException(errorMsg, ex);
            }
        }
        
        private void ConfigureDefaultChannels(int channelCount)
        {
            if (channelCount <= 0 || channelCount > 32)
                throw new ArgumentOutOfRangeException(nameof(channelCount), "El número de canales debe estar entre 1 y 32");

            LogInfo($"Configurando {channelCount} canales con rango ±10V...");
            
            int successCount = 0;
            for (int i = 0; i < channelCount; i++)
            {
                try
                {
                    _device.Channels[i].ValueRange = ValueRange.V_Neg10To10;
                    _device.Write(i, 0.0); // Inicializar a 0V
                    successCount++;
                }
                catch (Exception ex) when (IsRecoverableException(ex))
                {
                    LogWarning($"No se pudo configurar el canal {i}: {ex.Message}");
                    // Continuar con el siguiente canal
                }
            }
            
            if (successCount == 0)
                throw new InvalidOperationException("No se pudo configurar ningún canal");
                
            LogInfo($"{successCount} de {channelCount} canales configurados correctamente");
        }
        
        private void VerifyChannelConfiguration()
        {
            LogInfo("Verificando configuración de canales...");
            int validChannels = 0;
            
            for (int i = 0; i < _device.ChannelCount; i++)
            {
                try
                {
                    var range = _device.Channels[i].ValueRange;
                    LogDebug($"Canal {i}: Rango {range}");
                    validChannels++;
                }
                catch (Exception ex) when (IsRecoverableException(ex))
                {
                    LogWarning($"Error al verificar canal {i}: {ex.Message}");
                }
            }
            
            if (validChannels == 0)
                LogWarning("No se pudo verificar ningún canal. Verifique la conexión del dispositivo.");
            else
                LogInfo($"{validChannels} de {_device.ChannelCount} canales verificados correctamente");
        }
        
        private bool IsRecoverableException(Exception ex)
        {
            // Determina si la excepción es recuperable
            if (ex is ObjectDisposedException || 
                ex is NullReferenceException ||
                ex is AccessViolationException)
                return false;
                
            // Verificar si es una excepción de Win32
            if (ex is Win32Exception || ex is COMException)
                return true;
                
            // Verificar si es una excepción de operación no soportada
            if (ex is NotSupportedException || ex is NotImplementedException)
                return false;
                
            // Otras excepciones son consideradas recuperables
            return true;
        }
        
        #region Logging
        
        private void LogInfo(string message)
        {
            Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss.fff} - {message}");
        }
        
        private void LogWarning(string message)
        {
            Console.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss.fff} - {message}");
        }
        
        private void LogError(string message, Exception ex = null)
        {
            Console.Error.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss.fff} - {message}");
            if (ex != null)
            {
                Console.Error.WriteLine($"        Tipo: {ex.GetType().Name}");
                Console.Error.WriteLine($"        Mensaje: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"        Excepción interna: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                Console.Error.WriteLine($"        StackTrace: {ex.StackTrace}");
            }
        }
        
        private void LogDebug(string message)
        {
            // En producción, podrías querer habilitar esto solo en modo depuración
            // #if DEBUG
            Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss.fff} - {message}");
            // #endif
        }
        
        #endregion
        
        public void SetChannelValue(int channel, double value)
        {
            if (channel < 0 || channel >= _device.ChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channel), "Número de canal inválido");
                
            _signalGenerator.SetDcValue(channel, value);
        }
        
        public async Task RampChannelValue(int channel, double targetValue, int durationMs)
        {
            if (channel < 0 || channel >= _device.ChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channel), "Número de canal inválido");
                
            await _signalGenerator.SetDcValueAsync(channel, targetValue, durationMs);
        }
        
        public void ResetAllChannels()
        {
            _signalGenerator.ResetAllOutputs();
        }

        public void StartSignalGeneration(int channel, double frequency, double amplitude, double offset)
        {
            _signalGenerator.Start(channel, frequency, amplitude, offset);
        }

        public void StopSignalGeneration()
        {
            _signalGenerator.Stop();
        }

        public void SetSignalParameters(int channel, double frequency, double amplitude, double offset)
        {
            _signalGenerator.Start(channel, frequency, amplitude, offset);
        }
        
        public DeviceInfo GetDeviceInfo(string profilePath = null)
        {
            string deviceName = "Dispositivo no configurado";
            int channelCount = 0;
            string status = "No inicializado";
            
            try
            {
                deviceName = "PCIe-1824";
                
                // Verificar si el dispositivo está seleccionado
                try
                {
                    // Intentar acceder a una propiedad para verificar si está inicializado
                    var deviceDesc = _device.SelectedDevice.Description;
                }
                catch
                {
                    status = "Dispositivo no seleccionado o no accesible";
                    return new DeviceInfo($"{deviceName} - {status}", 0);
                }
                
                deviceName = $"{_device.SelectedDevice.Description} (ID: {_device.SelectedDevice.DeviceNumber})";
                
                // Obtener información de canales
                channelCount = _device.ChannelCount;
                
                // Verificar estado de inicialización
                status = _deviceInitialized ? "Inicializado" : "No inicializado";
                
                if (!_device.Initialized)
                {
                    status += " - Error de inicialización";
                }
                
                // Agregar información sobre el perfil
                string profileInfo = string.IsNullOrEmpty(profilePath) ? "Por defecto" : $"Personalizado ({Path.GetFileName(profilePath)})";
                
                // Construir el nombre del dispositivo con toda la información
                deviceName = $"{deviceName} - {status} (Canales: {channelCount}) - Perfil: {profileInfo}";
                
                // Mostrar información de diagnóstico
                Console.WriteLine("\n=== INFORMACIÓN DETALLADA DEL DISPOSITIVO ===");
                Console.WriteLine($"Modelo: {_device.SelectedDevice.Description}");
                Console.WriteLine($"Número de dispositivo: {_device.SelectedDevice.DeviceNumber}");
                Console.WriteLine($"Estado: {status}");
                Console.WriteLine($"Canales configurados: {channelCount}");
                Console.WriteLine($"Perfil: {profileInfo}");
                
                // Mostrar información de los primeros 8 canales como referencia
                int channelsToShow = Math.Min(channelCount, 8);
                if (channelsToShow > 0)
                {
                    Console.WriteLine("\nConfiguración de canales (primeros 8):");
                    for (int i = 0; i < channelsToShow; i++)
                    {
                        try
                        {
                            var range = _device.Channels[i].ValueRange;
                            Console.WriteLine($"  Canal {i}: {range}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Canal {i}: Error al leer configuración - {ex.GetType().Name}");
                        }
                    }
                }
                Console.WriteLine("==========================================\n");
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error al obtener información: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMsg += $" - {ex.InnerException.Message}";
                }
                deviceName = errorMsg;
                Console.WriteLine($"ERROR: {errorMsg}");
            }
            
            return new DeviceInfo(deviceName, channelCount);
        }

        public double GetCurrentValue(int channel)
        {
            // Nota: Esta es una implementación de ejemplo. 
            // En un caso real, podrías necesitar almacenar el último valor escrito
            // ya que el hardware podría no permitir leer el valor actual directamente.
            return 0.0; // Valor por defecto
        }
        
        /// <summary>
        /// Escribe un valor en un canal de salida analógica
        /// </summary>
        /// <param name="channel">Número de canal (0-31)</param>
        /// <param name="value">Valor de voltaje a escribir (-10V a +10V)</param>
        /// <exception cref="ObjectDisposedException">Si el controlador ya fue liberado</exception>
        /// <exception cref="InvalidOperationException">Si el dispositivo no está inicializado</exception>
        /// <exception cref="ArgumentOutOfRangeException">Si el canal o valor están fuera de rango</exception>
        /// <exception cref="Win32Exception">Si ocurre un error en la comunicación con el dispositivo</exception>
        public void WriteToChannel(int channel, double value)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DAQController));
                
            if (!_deviceInitialized)
                throw new InvalidOperationException("El dispositivo no está inicializado. Llame al método Initialize() primero.");
            
            if (channel < 0 || channel >= _device.ChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channel), $"El canal {channel} está fuera de rango. Debe estar entre 0 y {_device.ChannelCount - 1}");
            
            // Validar rango de voltaje
            if (value < -10.0 || value > 10.0)
                throw new ArgumentOutOfRangeException(nameof(value), $"El valor {value}V está fuera de rango. Debe estar entre -10V y +10V");
            
            try
            {
                _device.Write(channel, value);
                LogDebug($"Canal {channel} actualizado a {value}V");
            }
            catch (Exception ex) when (IsRecoverableException(ex))
            {
                string errorMsg = $"Error al escribir {value}V en el canal {channel}";
                LogError(errorMsg, ex);
                throw new Win32Exception(errorMsg, ex);
            }
        }
        
        /// <summary>
        /// Libera los recursos utilizados por el controlador DAQ
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Liberar recursos administrados
                    try
                    {
                        if (_device != null)
                        {
                            try
                            {
                                // Intentar poner todos los canales a 0V antes de cerrar
                                for (int i = 0; i < _device.ChannelCount; i++)
                                {
                                    try
                                    {
                                        _device.Write(i, 0.0);
                                    }
                                    catch { /* Ignorar errores al limpiar */ }
                                }
                            }
                            finally
                            {
                                _device.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // No lanzar excepciones en Dispose
                        LogError("Error al liberar recursos", ex);
                    }
                }

                _disposed = true;
                _deviceInitialized = false;
            }
        }

        ~DAQController()
        {
            Dispose(false);
        }
    }

    public class DeviceInfo
    {
        public string Name { get; set; }
        public int Channels { get; set; }

        public DeviceInfo(string name, int channels)
        {
            Name = name;
            Channels = channels;
        }
    }
}

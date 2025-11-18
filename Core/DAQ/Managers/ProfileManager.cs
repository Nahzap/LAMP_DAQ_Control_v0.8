using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Managers
{
    /// <summary>
    /// Manages device profiles for DAQ devices
    /// </summary>
    public class ProfileManager : IProfileManager
    {
        private const string DEFAULT_PROFILE_EXTENSION = "*.xml";
        private const string PROFILES_DIRECTORY = "..\\..\\Core\\DAQ\\Profiles";
        private const string DEFAULT_PROFILE_NAME = "PCIe1824_prof_v1.xml";

        private readonly Dictionary<string, DeviceProfile> _availableProfiles;
        private readonly IDeviceManager _deviceManager;
        private readonly ILogger _logger;
        private DeviceProfile _activeProfile;

        public ProfileManager(IDeviceManager deviceManager, ILogger logger = null)
        {
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _logger = logger ?? new ConsoleLogger();
            _availableProfiles = new Dictionary<string, DeviceProfile>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                // Initialize default profiles
                InitializeDefaultProfiles();
                
                // Scan for available profiles
                ScanForProfiles();
                
                _logger.Info("Profile manager created successfully");
            }
            catch (Exception ex)
            {
                const string errorMsg = "Error creating profile manager";
                _logger.Error(errorMsg, ex);
                throw new Exception(errorMsg, ex);
            }
        }

        public string ActiveProfileName => _activeProfile?.Name ?? "Default";

        public IReadOnlyCollection<string> AvailableProfiles => _availableProfiles.Keys.ToList();

        public DeviceProfile ActiveProfile => _activeProfile;

        /// <summary>
        /// Carga un perfil desde la carpeta Core/DAQ/Profiles
        /// </summary>
        /// <param name="profileName">Nombre del perfil (sin ruta). Si es null o vacío, se usará el perfil por defecto</param>
        /// <returns>True si el perfil se cargó correctamente, False en caso contrario</returns>
        /// <exception cref="FileNotFoundException">Si el perfil por defecto no se encuentra</exception>
        public bool TryLoadProfile(string profileName = null)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string fullPath;
            
            // Si no se especifica un perfil, usar el perfil por defecto
            if (string.IsNullOrEmpty(profileName))
            {
                profileName = DEFAULT_PROFILE_NAME;
                _logger.Info($"No se especificó un perfil, usando el perfil por defecto: {profileName}");
            }
            
            // Asegurar que tenga extensión .xml
            if (!Path.HasExtension(profileName))
            {
                profileName += ".xml";
            }
            
            // Construir la ruta completa al perfil en Core/DAQ/Profiles
            fullPath = Path.Combine(baseDir, PROFILES_DIRECTORY, profileName);
            _logger.Info($"Buscando perfil en: {fullPath}");
            
            if (!File.Exists(fullPath))
            {
                string errorMsg = $"Perfil no encontrado: {fullPath}";
                _logger.Error(errorMsg);
                
                // Si es el perfil por defecto, lanzar excepción
                if (profileName.Equals(DEFAULT_PROFILE_NAME, StringComparison.OrdinalIgnoreCase))
                {
                    throw new FileNotFoundException(errorMsg);
                }
                
                return false;
            }
            
            try
            {
                // Validate the profile before loading
                if (!ValidateProfile(fullPath, _deviceManager.DeviceModel))
                {
                    _logger.Error($"Validación de perfil fallida para: {fullPath}");
                    return false;
                }

                _deviceManager.Device.LoadProfile(fullPath);
                _logger.Info($"Perfil cargado exitosamente desde: {fullPath}");
                
                // Update active profile if it's a known one
                string profileBaseName = Path.GetFileNameWithoutExtension(fullPath);
                if (_availableProfiles.TryGetValue(profileBaseName, out var profile))
                {
                    _activeProfile = profile;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error al cargar perfil {fullPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Carga el perfil por defecto
        /// </summary>
        /// <returns>True si el perfil se cargó correctamente</returns>
        /// <exception cref="FileNotFoundException">Si el perfil por defecto no se encuentra</exception>
        public bool TryLoadDefaultProfile()
        {
            // Simplemente llamamos a TryLoadProfile sin especificar un nombre de perfil
            // para que use el perfil por defecto
            return TryLoadProfile(null);
        }

        public DeviceProfile MatchDeviceToProfile(string deviceDescription)
        {
            if (string.IsNullOrEmpty(deviceDescription))
                return null;
                
            // Try to find a matching profile by hardware identifiers
            return _availableProfiles.Values.FirstOrDefault(profile => 
                profile.HardwareIdentifiers.Any(id => 
                    deviceDescription.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public ValueRange GetDefaultRange()
        {
            return _activeProfile?.DefaultRange ?? ValueRange.V_Neg10To10;
        }

        public void InitializeDefaultProfiles()
        {
            // Add default profiles for known devices
            _availableProfiles["PCIe-1824"] = new DeviceProfile
            {
                Name = "PCIe-1824",
                Description = "Advantech PCIe-1824 16-bit Multifunction DAQ",
                FileName = "PCIe1824_prof_v1.xml",
                HardwareIdentifiers = new[] { "PCIe-1824", "1824" },
                DefaultRange = ValueRange.V_Neg10To10,
                ExpectedChannelCount = 4
            };

            _availableProfiles["PCI-1735U"] = new DeviceProfile
            {
                Name = "PCI-1735U",
                Description = "Advantech PCI-1735U 16-bit Multifunction DAQ",
                FileName = "PCI1735U_prof_v1.xml",
                HardwareIdentifiers = new[] { "PCI-1735U", "1735U" },
                DefaultRange = ValueRange.V_Neg10To10,
                ExpectedChannelCount = 4
            };
        }

        public void ScanForProfiles()
        {
            try
            {
                // Buscar en el directorio de la aplicación y sus subdirectorios
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                List<string> profileFiles = new List<string>();
                
                // Buscar en el directorio base
                profileFiles.AddRange(Directory.GetFiles(baseDir, DEFAULT_PROFILE_EXTENSION));
                
                // Buscar en el directorio Profiles si existe
                string profilesDir = Path.Combine(baseDir, "Profiles");
                if (Directory.Exists(profilesDir))
                {
                    profileFiles.AddRange(Directory.GetFiles(profilesDir, DEFAULT_PROFILE_EXTENSION));
                }
                
                // Buscar en Core/DAQ/Profiles si existe
                string coreProfilesDir = Path.Combine(baseDir, "..\\..\\Core\\DAQ\\Profiles");
                if (Directory.Exists(coreProfilesDir))
                {
                    profileFiles.AddRange(Directory.GetFiles(coreProfilesDir, DEFAULT_PROFILE_EXTENSION));
                }
                
                // Procesar todos los archivos encontrados
                foreach (string profileFile in profileFiles)
                {
                    AddProfileFromFile(profileFile);
                    _logger.Info($"Found profile: {Path.GetFileName(profileFile)}");
                }
                
                _logger.Info($"Found {_availableProfiles.Count} available profiles");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error scanning for profiles: {ex.Message}");
            }
        }

        private void AddProfileFromFile(string profileFile)
        {
            try
            {
                string fileName = Path.GetFileName(profileFile);
                string profileName = Path.GetFileNameWithoutExtension(profileFile);
                
                if (!_availableProfiles.ContainsKey(profileName))
                {
                    _availableProfiles[profileName] = new DeviceProfile
                    {
                        Name = profileName,
                        FileName = fileName,
                        Description = $"Custom profile: {profileName}",
                        HardwareIdentifiers = Array.Empty<string>(),
                        DefaultRange = ValueRange.V_Neg10To10
                    };
                    _logger.Debug($"Discovered custom profile: {profileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Error processing profile {profileFile}: {ex.Message}");
            }
        }

        public bool ValidateProfile(string profilePath, string deviceModel)
        {
            try
            {
                var doc = XDocument.Load(profilePath);
                if (doc.Root == null || doc.Root.Name != "DAQNavi")
                {
                    _logger.Warn($"Invalid profile format: Root element 'DAQNavi' not found (found: {doc.Root?.Name})");
                    return false;
                }
                
                // Verificar que tenga al menos un elemento de configuración de dispositivo
                var hasDeviceConfig = doc.Root.Element("DaqDevice") != null ||
                                     doc.Root.Element("DaqAo") != null ||
                                     doc.Root.Element("DaqDio") != null;
                
                if (!hasDeviceConfig)
                {
                    _logger.Warn("Invalid profile format: No device configuration found");
                    return false;
                }
                
                _logger.Info($"Profile validated successfully: {profilePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Profile validation error: {ex.Message}");
                return false;
            }
        }
    }
}

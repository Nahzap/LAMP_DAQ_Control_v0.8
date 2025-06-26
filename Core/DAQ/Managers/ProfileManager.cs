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
        private const string PROFILES_DIRECTORY = "Profiles";
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

        public bool TryLoadProfile(string profilePath)
        {
            if (string.IsNullOrEmpty(profilePath))
            {
                _logger.Debug("No profile path provided, using default configuration");
                return false;
            }

            string fullPath = profilePath;
            
            // If it's just a profile name, try to find it in the profiles directory
            if (!Path.IsPathRooted(profilePath) && !profilePath.Contains(Path.DirectorySeparatorChar))
            {
                string profilesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PROFILES_DIRECTORY);
                fullPath = Path.Combine(profilesDir, profilePath);
                
                // Ensure the file has an extension
                if (!Path.HasExtension(fullPath))
                {
                    fullPath += ".xml";
                }
            }

            if (!File.Exists(fullPath))
            {
                _logger.Warn($"Profile file not found: {fullPath}");
                return false;
            }

            try
            {
                // Validate the profile before loading
                if (!ValidateProfile(fullPath, _deviceManager.DeviceModel))
                {
                    _logger.Warn($"Profile validation failed for: {fullPath}");
                    return false;
                }

                _deviceManager.Device.LoadProfile(fullPath);
                _logger.Info($"Profile loaded successfully from: {fullPath}");
                
                // Update active profile if it's a known one
                string profileName = Path.GetFileNameWithoutExtension(fullPath);
                if (_availableProfiles.TryGetValue(profileName, out var profile))
                {
                    _activeProfile = profile;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading profile {fullPath}: {ex.Message}");
                return false;
            }
        }

        public bool TryLoadDefaultProfile()
        {
            string defaultProfile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DEFAULT_PROFILE_NAME);
            return TryLoadProfile(defaultProfile);
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
                string profilesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PROFILES_DIRECTORY);
                
                if (!Directory.Exists(profilesDir))
                {
                    _logger.Info($"Profiles directory not found: {profilesDir}");
                    return;
                }

                foreach (string profileFile in Directory.GetFiles(profilesDir, DEFAULT_PROFILE_EXTENSION))
                {
                    AddProfileFromFile(profileFile);
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
                if (doc.Root == null || doc.Root.Name != "Profile")
                {
                    _logger.Warn("Invalid profile format: Root element 'Profile' not found");
                    return false;
                }
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

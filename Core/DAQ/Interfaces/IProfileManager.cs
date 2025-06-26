using System.Collections.Generic;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces
{
    /// <summary>
    /// Interface for managing device profiles
    /// </summary>
    public interface IProfileManager
    {
        /// <summary>
        /// Gets the active profile name, if any
        /// </summary>
        string ActiveProfileName { get; }

        /// <summary>
        /// Gets a list of available profile names
        /// </summary>
        IReadOnlyCollection<string> AvailableProfiles { get; }

        /// <summary>
        /// Gets the active device profile
        /// </summary>
        DeviceProfile ActiveProfile { get; }

        /// <summary>
        /// Initializes default profiles
        /// </summary>
        void InitializeDefaultProfiles();

        /// <summary>
        /// Scans for available profiles in the profiles directory
        /// </summary>
        void ScanForProfiles();

        /// <summary>
        /// Tries to load a profile from the specified path
        /// </summary>
        /// <param name="profilePath">Path to the profile file</param>
        /// <returns>True if profile was loaded successfully, false otherwise</returns>
        bool TryLoadProfile(string profilePath);

        /// <summary>
        /// Tries to load the default profile
        /// </summary>
        /// <returns>True if profile was loaded successfully, false otherwise</returns>
        bool TryLoadDefaultProfile();

        /// <summary>
        /// Validates a profile file
        /// </summary>
        /// <param name="profilePath">Path to the profile file</param>
        /// <param name="deviceModel">Device model to validate against</param>
        /// <returns>True if profile is valid, false otherwise</returns>
        bool ValidateProfile(string profilePath, string deviceModel);

        /// <summary>
        /// Matches a device description to a profile
        /// </summary>
        /// <param name="deviceDescription">Device description to match</param>
        /// <returns>Matching profile or null if no match found</returns>
        DeviceProfile MatchDeviceToProfile(string deviceDescription);

        /// <summary>
        /// Gets the default value range for channels
        /// </summary>
        /// <returns>Default value range</returns>
        ValueRange GetDefaultRange();
    }
}

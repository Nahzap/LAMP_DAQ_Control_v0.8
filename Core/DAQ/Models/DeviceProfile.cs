using Automation.BDaq;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Models
{
    /// <summary>
    /// Represents a DAQ device profile with its properties and validation rules
    /// </summary>
    public class DeviceProfile
    {
        /// <summary>
        /// Gets or sets the name of the profile
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the device
        /// </summary>
        public string Description { get; set; }


        /// <summary>
        /// Gets or sets the filename of the profile
        /// </summary>
        public string FileName { get; set; }


        /// <summary>
        /// Gets or sets the hardware identifiers for this profile
        /// </summary>
        public string[] HardwareIdentifiers { get; set; }

        /// <summary>
        /// Gets or sets the default value range for channels
        /// </summary>
        public ValueRange DefaultRange { get; set; } = ValueRange.V_Neg10To10;

        /// <summary>
        /// Gets or sets the expected number of channels for this device
        /// </summary>
        public int ExpectedChannelCount { get; set; } = 4;
    }
}

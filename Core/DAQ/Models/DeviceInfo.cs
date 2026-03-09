namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Models
{
    /// <summary>
    /// Represents information about a DAQ device
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>
        /// Gets the number of channels available on the device
        /// </summary>
        public int Channels => ChannelCount;
        
        /// <summary>
        /// Gets the name of the device
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the number of channels available on the device
        /// </summary>
        public int ChannelCount { get; }

        /// <summary>
        /// Gets or sets additional device-specific information
        /// </summary>
        public string AdditionalInfo { get; set; }
        
        /// <summary>
        /// Gets or sets the type of device (Analog or Digital)
        /// </summary>
        public DeviceType DeviceType { get; set; } = DeviceType.Analog;

        /// <summary>
        /// Initializes a new instance of the DeviceInfo class
        /// </summary>
        /// <param name="name">The name of the device</param>
        /// <param name="channelCount">The number of channels available</param>
        public DeviceInfo(string name, int channelCount)
        {
            Name = name ?? throw new System.ArgumentNullException(nameof(name));
            ChannelCount = channelCount >= 0 ? channelCount : throw new System.ArgumentOutOfRangeException(nameof(channelCount));
        }

        /// <summary>
        /// Returns a string that represents the current object
        /// </summary>
        public override string ToString()
        {
            return $"{Name} (Channels: {ChannelCount})" + 
                   (string.IsNullOrEmpty(AdditionalInfo) ? "" : $" - {AdditionalInfo}");
        }
    }
}

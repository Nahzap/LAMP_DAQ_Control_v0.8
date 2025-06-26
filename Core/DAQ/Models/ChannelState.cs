namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Models
{
    /// <summary>
    /// Represents the current state of a DAQ channel
    /// </summary>
    public class ChannelState
    {
        /// <summary>
        /// Gets the channel number
        /// </summary>
        public int ChannelNumber { get; }

        /// <summary>
        /// Gets the current value in volts
        /// </summary>
        public double CurrentValue { get; }

        /// <summary>
        /// Gets the value range of the channel
        /// </summary>
        public string ValueRange { get; }

        /// <summary>
        /// Gets a value indicating whether the channel is currently active
        /// </summary>
        public bool IsActive { get; }

        public ChannelState(int channelNumber, double currentValue, string valueRange, bool isActive)
        {
            ChannelNumber = channelNumber;
            CurrentValue = currentValue;
            ValueRange = valueRange;
            IsActive = isActive;
        }
    }
}

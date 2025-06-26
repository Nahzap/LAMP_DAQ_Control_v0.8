using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces
{
    /// <summary>
    /// Interface for managing DAQ channels
    /// </summary>
    public interface IChannelManager
    {
        /// <summary>
        /// Gets the current state of all channels
        /// </summary>
        /// <param name="signalGenerator">Signal generator to check active channels</param>
        /// <returns>Collection of channel states</returns>
        IReadOnlyCollection<ChannelState> GetChannelStates(ISignalGenerator signalGenerator);

        /// <summary>
        /// Writes a voltage value to the specified channel
        /// </summary>
        /// <param name="channel">Channel number (0-based)</param>
        /// <param name="value">Value to write (in Volts)</param>
        void WriteVoltage(int channel, double value);

        /// <summary>
        /// Resets all channels to their default values (0V or 4mA)
        /// </summary>
        void ResetAllChannels();

        /// <summary>
        /// Validates a channel number
        /// </summary>
        /// <param name="channel">Channel number to validate</param>
        void ValidateChannelNumber(int channel);
    }
}

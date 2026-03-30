using System;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.HAL
{
    /// <summary>
    /// Hardware Abstraction Layer for analog output.
    /// Pure hardware access — no logic, no timers.
    /// Writes voltage values to DAC channels (PCIe-1824: 32 channels, 16-bit, 0-10V).
    /// </summary>
    public interface IAnalogHal : IDisposable
    {
        /// <summary>
        /// Whether the analog device is initialized and ready
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Number of available analog output channels
        /// </summary>
        int ChannelCount { get; }

        /// <summary>
        /// Writes a single voltage to one channel.
        /// </summary>
        /// <param name="channel">Channel index (0-based)</param>
        /// <param name="voltage">Voltage value (0-10V)</param>
        void WriteSingle(int channel, double voltage);

        /// <summary>
        /// Writes voltages to multiple channels using a bitmask.
        /// Only channels whose bit is set in activeMask are updated,
        /// saving bus cycles for inactive channels.
        /// </summary>
        /// <param name="voltages">Array of voltages (indexed by channel number)</param>
        /// <param name="activeMask">Bitmask indicating which channels to update (bit N = channel N)</param>
        void WriteOutputs(double[] voltages, uint activeMask);

        /// <summary>
        /// MED-03: Gets the voltage range configured for a specific channel.
        /// Returns the min and max voltage values (e.g., -10.0/+10.0 for V_Neg10To10).
        /// </summary>
        /// <param name="channel">Channel index</param>
        /// <param name="minVoltage">Minimum voltage output</param>
        /// <param name="maxVoltage">Maximum voltage output</param>
        /// <returns>true if range info is available</returns>
        bool GetChannelVoltageRange(int channel, out double minVoltage, out double maxVoltage);
    }
}

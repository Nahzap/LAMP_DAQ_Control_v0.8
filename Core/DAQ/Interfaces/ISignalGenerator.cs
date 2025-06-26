using System;
using System.Threading.Tasks;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces
{
    /// <summary>
    /// Defines the interface for signal generation functionality
    /// </summary>
    public interface ISignalGenerator : IDisposable
    {
        /// <summary>
        /// Starts signal generation on the specified channel
        /// </summary>
        void Start(int channel, double frequency, double amplitude, double offset);
        
        /// <summary>
        /// Stops signal generation
        /// </summary>
        void Stop();
        
        /// <summary>
        /// Sets a DC value on the specified channel
        /// </summary>
        void SetDcValue(int channel, double value);
        
        /// <summary>
        /// Ramps a channel to a target value over the specified duration
        /// </summary>
        Task SetDcValueAsync(int channel, double targetValue, int durationMs);
        
        /// <summary>
        /// Resets all outputs to their default state (0V)
        /// </summary>
        void ResetAllOutputs();
        
        /// <summary>
        /// Checks if the specified channel is currently active (outputting a signal)
        /// </summary>
        /// <param name="channel">Channel number to check</param>
        /// <returns>True if the channel is active, false otherwise</returns>
        bool IsChannelActive(int channel);
    }
}

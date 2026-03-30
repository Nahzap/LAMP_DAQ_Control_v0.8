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
        /// Stops all signal generation
        /// </summary>
        void Stop();
        
        /// <summary>
        /// Stops signal generation on a specific channel only
        /// </summary>
        /// <param name="channel">Channel to stop</param>
        void StopChannel(int channel);
        
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
        
        /// <summary>
        /// HIGH-04: Prepares a phase synchronization barrier for N parallel waveforms.
        /// Must be called before launching parallel waveform starts.
        /// </summary>
        void PreparePhaseBarrier(int participantCount);
        
        /// <summary>
        /// HIGH-04: Clears the phase synchronization barrier after waveforms have started.
        /// </summary>
        void ClearPhaseBarrier();
    }
}

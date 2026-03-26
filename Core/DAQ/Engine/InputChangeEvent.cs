using System.Diagnostics;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Engine
{
    /// <summary>
    /// Lightweight struct for input change events.
    /// Deposited into ConcurrentQueue by the InputPoller.
    /// Consumed by the StateGrid/LogicPipeline.
    /// </summary>
    public struct InputChangeEvent
    {
        /// <summary>
        /// Full 32-bit state of all digital inputs at the moment of change.
        /// </summary>
        public uint NewState;

        /// <summary>
        /// XOR delta: which bits changed since last read.
        /// </summary>
        public uint Delta;

        /// <summary>
        /// High-resolution timestamp (Stopwatch ticks) of the change detection.
        /// </summary>
        public long Timestamp;
    }
}

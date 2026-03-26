using System;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.HAL
{
    /// <summary>
    /// Hardware Abstraction Layer for digital I/O.
    /// Pure hardware access — no logic, no timers.
    /// Reads/writes 32-bit port state (4 ports × 8 bits).
    /// </summary>
    public interface IDigitalHal : IDisposable
    {
        /// <summary>
        /// Whether the digital device is initialized and ready
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Reads all 4 input ports as a single 32-bit word.
        /// Bits [0..7] = Port 0, [8..15] = Port 1, [16..23] = Port 2, [24..31] = Port 3.
        /// </summary>
        uint ReadInputs();

        /// <summary>
        /// Writes all 4 output ports from a single 32-bit word.
        /// Only ports whose corresponding bits differ from the current state are written.
        /// </summary>
        /// <param name="state">Full 32-bit output state</param>
        void WriteOutputs(uint state);

        /// <summary>
        /// Writes only the ports indicated by the mask.
        /// For each bit set in activeMask, the corresponding port byte from state is written.
        /// Bit 0 = Port 0, Bit 1 = Port 1, Bit 2 = Port 2, Bit 3 = Port 3.
        /// </summary>
        /// <param name="state">Full 32-bit output state</param>
        /// <param name="portMask">Which ports to actually write (bits 0-3)</param>
        void WriteOutputsMasked(uint state, byte portMask);

        /// <summary>
        /// Reads all 4 input ports into the provided buffer (4 bytes).
        /// Zero-allocation path for hot loops.
        /// </summary>
        /// <param name="buffer">Pre-allocated 4-byte buffer</param>
        /// <returns>true if read succeeded</returns>
        bool ReadInputsRaw(byte[] buffer);
    }
}

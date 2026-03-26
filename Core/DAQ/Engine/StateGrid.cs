using System;
using System.Diagnostics;
using System.Threading;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Engine
{
    /// <summary>
    /// Central state registry with atomic bitmask operations.
    /// Maintains the "absolute truth" of which channels are active.
    /// All reads/writes use Interlocked for lock-free thread safety.
    /// 
    /// Bit layout (32 bits each):
    ///   Digital: bit N = digital channel N (port=N/8, bit=N%8)
    ///   Analog:  bit N = analog channel N (0-31)
    /// </summary>
    public class StateGrid
    {
        // --- Input state (from hardware, set by InputPoller) ---
        private long _activeInputMask;

        // --- Output requirements (set by LogicPipeline) ---
        private long _requiredDigitalOutputMask;
        private long _requiredAnalogOutputMask;

        // --- Full output state values ---
        private long _digitalOutputState;
        private readonly double[] _analogVoltageBuffer;
        private readonly double[] _analogVoltageFront;
        private readonly object _analogBufferLock = new object();
        private int _analogBufferDirty;

        // --- Timing ---
        private long _lastInputChangeTimestamp;
        private long _lastOutputWriteTimestamp;

        private readonly ILogger _logger;
        private readonly int _analogChannelCount;

        public StateGrid(int analogChannelCount, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _analogChannelCount = Math.Max(analogChannelCount, 32);

            _analogVoltageBuffer = new double[_analogChannelCount];
            _analogVoltageFront = new double[_analogChannelCount];

            _logger.Info($"[StateGrid] Initialized with {analogChannelCount} analog channels");
        }

        #region Input State (written by InputPoller, read by LogicPipeline)

        /// <summary>
        /// Gets which digital input channels currently have signal.
        /// Lock-free atomic read.
        /// </summary>
        public uint ActiveInputMask
        {
            get { return (uint)Interlocked.Read(ref _activeInputMask); }
        }

        /// <summary>
        /// Updates the active input mask. Called by InputPoller when change detected.
        /// Returns the XOR delta (which bits changed).
        /// </summary>
        public uint UpdateInputState(uint newState)
        {
            uint previous = (uint)Interlocked.Exchange(ref _activeInputMask, newState);
            Interlocked.Exchange(ref _lastInputChangeTimestamp, Stopwatch.GetTimestamp());
            return previous ^ newState;
        }

        /// <summary>
        /// Timestamp of last input change (Stopwatch ticks).
        /// </summary>
        public long LastInputChangeTimestamp
        {
            get { return Interlocked.Read(ref _lastInputChangeTimestamp); }
        }

        #endregion

        #region Output Masks (written by LogicPipeline, read by Dispatcher)

        /// <summary>
        /// Which digital outputs need to be updated in the next cycle.
        /// </summary>
        public uint RequiredDigitalOutputMask
        {
            get { return (uint)Interlocked.Read(ref _requiredDigitalOutputMask); }
            set { Interlocked.Exchange(ref _requiredDigitalOutputMask, value); }
        }

        /// <summary>
        /// Which analog outputs need to be updated in the next cycle.
        /// </summary>
        public uint RequiredAnalogOutputMask
        {
            get { return (uint)Interlocked.Read(ref _requiredAnalogOutputMask); }
            set { Interlocked.Exchange(ref _requiredAnalogOutputMask, value); }
        }

        #endregion

        #region Digital Output State

        /// <summary>
        /// Gets/sets the full 32-bit digital output state (4 ports × 8 bits).
        /// </summary>
        public uint DigitalOutputState
        {
            get { return (uint)Interlocked.Read(ref _digitalOutputState); }
        }

        /// <summary>
        /// Sets specific bits in the digital output state.
        /// Uses atomic compare-and-swap loop.
        /// </summary>
        public void SetDigitalBits(uint bitsToSet)
        {
            long initial, desired;
            do
            {
                initial = Interlocked.Read(ref _digitalOutputState);
                desired = initial | (long)bitsToSet;
            } while (Interlocked.CompareExchange(ref _digitalOutputState, desired, initial) != initial);
        }

        /// <summary>
        /// Clears specific bits in the digital output state.
        /// Uses atomic compare-and-swap loop.
        /// </summary>
        public void ClearDigitalBits(uint bitsToClear)
        {
            long initial, desired;
            do
            {
                initial = Interlocked.Read(ref _digitalOutputState);
                desired = initial & ~(long)bitsToClear;
            } while (Interlocked.CompareExchange(ref _digitalOutputState, desired, initial) != initial);
        }

        /// <summary>
        /// Atomically replaces the entire digital output state.
        /// </summary>
        public void SetDigitalOutputState(uint fullState)
        {
            Interlocked.Exchange(ref _digitalOutputState, fullState);
        }

        /// <summary>
        /// Sets a single digital bit (port*8 + bit).
        /// </summary>
        public void SetDigitalBit(int port, int bit, bool value)
        {
            uint mask = 1u << (port * 8 + bit);
            if (value)
                SetDigitalBits(mask);
            else
                ClearDigitalBits(mask);

            // Mark port as requiring update
            long initial, desired;
            do
            {
                initial = Interlocked.Read(ref _requiredDigitalOutputMask);
                desired = initial | mask;
            } while (Interlocked.CompareExchange(ref _requiredDigitalOutputMask, desired, initial) != initial);
        }

        #endregion

        #region Analog Output Buffer (Double Buffering)

        /// <summary>
        /// Sets a voltage in the back buffer for a specific channel.
        /// Thread-safe with lock (analog writes are less latency-critical than digital).
        /// </summary>
        public void SetAnalogVoltage(int channel, double voltage)
        {
            if (channel < 0 || channel >= _analogChannelCount)
                return;

            lock (_analogBufferLock)
            {
                _analogVoltageBuffer[channel] = voltage;
            }

            // Mark channel as requiring update
            long initial, desired;
            uint channelBit = 1u << channel;
            do
            {
                initial = Interlocked.Read(ref _requiredAnalogOutputMask);
                desired = initial | channelBit;
            } while (Interlocked.CompareExchange(ref _requiredAnalogOutputMask, desired, initial) != initial);

            Interlocked.Exchange(ref _analogBufferDirty, 1);
        }

        /// <summary>
        /// Swaps the analog buffer (double buffering).
        /// Copies back buffer to front buffer and returns the front buffer.
        /// Called by the OutputMetronome when freezing state.
        /// </summary>
        public double[] SwapAnalogBuffer()
        {
            if (Interlocked.CompareExchange(ref _analogBufferDirty, 0, 1) == 1)
            {
                lock (_analogBufferLock)
                {
                    Array.Copy(_analogVoltageBuffer, _analogVoltageFront, _analogChannelCount);
                }
            }
            return _analogVoltageFront;
        }

        /// <summary>
        /// Gets the current voltage for a channel (from front buffer).
        /// Used for reading current state without affecting the pipeline.
        /// </summary>
        public double GetAnalogVoltage(int channel)
        {
            if (channel < 0 || channel >= _analogChannelCount)
                return 0.0;

            lock (_analogBufferLock)
            {
                return _analogVoltageBuffer[channel];
            }
        }

        #endregion

        #region Output Timing

        /// <summary>
        /// Records when the last hardware write occurred.
        /// </summary>
        public void RecordOutputWrite()
        {
            Interlocked.Exchange(ref _lastOutputWriteTimestamp, Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Timestamp of last output write (Stopwatch ticks).
        /// </summary>
        public long LastOutputWriteTimestamp
        {
            get { return Interlocked.Read(ref _lastOutputWriteTimestamp); }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Clears all output masks (no pending writes).
        /// Called after Dispatcher has completed a write cycle.
        /// </summary>
        public void ClearOutputMasks()
        {
            Interlocked.Exchange(ref _requiredDigitalOutputMask, 0);
            Interlocked.Exchange(ref _requiredAnalogOutputMask, 0);
        }

        /// <summary>
        /// Resets all state to zero.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _activeInputMask, 0);
            Interlocked.Exchange(ref _requiredDigitalOutputMask, 0);
            Interlocked.Exchange(ref _requiredAnalogOutputMask, 0);
            Interlocked.Exchange(ref _digitalOutputState, 0);
            Interlocked.Exchange(ref _analogBufferDirty, 0);

            lock (_analogBufferLock)
            {
                Array.Clear(_analogVoltageBuffer, 0, _analogChannelCount);
                Array.Clear(_analogVoltageFront, 0, _analogChannelCount);
            }

            _logger.Info("[StateGrid] Reset to zero");
        }

        #endregion
    }
}

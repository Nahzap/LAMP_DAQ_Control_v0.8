namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.Models
{
    /// <summary>
    /// Types of signal events that can be used in sequences
    /// </summary>
    public enum SignalEventType
    {
        /// <summary>
        /// DC voltage output (constant)
        /// </summary>
        DC,

        /// <summary>
        /// Ramp from current value to target value
        /// </summary>
        Ramp,

        /// <summary>
        /// Continuous waveform generation (sine, square, triangle)
        /// </summary>
        Waveform,

        /// <summary>
        /// Digital output pulse
        /// </summary>
        DigitalPulse,

        /// <summary>
        /// Digital output state (ON/OFF)
        /// </summary>
        DigitalState,

        /// <summary>
        /// Digital pulse train (TTL with frequency and duty cycle)
        /// </summary>
        PulseTrain,

        /// <summary>
        /// Wait/delay (no output change)
        /// </summary>
        Wait
    }
}

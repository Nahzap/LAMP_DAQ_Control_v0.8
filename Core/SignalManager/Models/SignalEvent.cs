using System;
using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.Models
{
    /// <summary>
    /// Represents a single signal event in a sequence
    /// </summary>
    public class SignalEvent
    {
        /// <summary>
        /// Unique identifier for this event
        /// </summary>
        public string EventId { get; set; }

        /// <summary>
        /// Human-readable name for this event
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Time when this event should start (relative to sequence start)
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// Duration of this event
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Channel number (0-31)
        /// </summary>
        public int Channel { get; set; }

        /// <summary>
        /// Device type (Analog or Digital)
        /// </summary>
        public DeviceType DeviceType { get; set; }

        /// <summary>
        /// Device model (e.g., "PCI-1735U", "PCIE-1824")
        /// CRITICAL: Required to distinguish between multiple devices
        /// </summary>
        public string DeviceModel { get; set; }

        /// <summary>
        /// Type of signal event
        /// </summary>
        public SignalEventType EventType { get; set; }

        /// <summary>
        /// Parameters specific to this event type
        /// Key-value pairs for flexibility
        /// </summary>
        public Dictionary<string, double> Parameters { get; set; }

        /// <summary>
        /// Optional description of this event
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Color for visualization (hex format: #RRGGBB)
        /// </summary>
        public string Color { get; set; }

        public SignalEvent()
        {
            EventId = Guid.NewGuid().ToString();
            Parameters = new Dictionary<string, double>();
            Color = "#4A90E2"; // Default blue
        }

        /// <summary>
        /// Gets the end time of this event
        /// </summary>
        public TimeSpan EndTime => StartTime + Duration;

        /// <summary>
        /// Validates this event
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (Channel < 0 || Channel > 31)
            {
                errorMessage = $"Invalid channel: {Channel}. Must be 0-31.";
                return false;
            }

            if (Duration.TotalMilliseconds < 0)
            {
                errorMessage = "Duration cannot be negative.";
                return false;
            }

            if (StartTime.TotalMilliseconds < 0)
            {
                errorMessage = "Start time cannot be negative.";
                return false;
            }

            // Validate parameters based on event type
            switch (EventType)
            {
                case SignalEventType.DC:
                    if (!Parameters.ContainsKey("voltage"))
                    {
                        errorMessage = "DC event requires 'voltage' parameter.";
                        return false;
                    }
                    if (DeviceType == DeviceType.Analog && (Parameters["voltage"] < 0 || Parameters["voltage"] > 10))
                    {
                        errorMessage = "Voltage must be 0-10V for analog devices.";
                        return false;
                    }
                    break;

                case SignalEventType.Ramp:
                    if (!Parameters.ContainsKey("endVoltage"))
                    {
                        errorMessage = "Ramp event requires 'endVoltage' parameter.";
                        return false;
                    }
                    break;

                case SignalEventType.Waveform:
                    if (!Parameters.ContainsKey("frequency") || !Parameters.ContainsKey("amplitude"))
                    {
                        errorMessage = "Waveform event requires 'frequency' and 'amplitude' parameters.";
                        return false;
                    }
                    if (Parameters["frequency"] <= 0)
                    {
                        errorMessage = "Frequency must be > 0.";
                        return false;
                    }
                    break;
            }

            errorMessage = null;
            return true;
        }
    }
}

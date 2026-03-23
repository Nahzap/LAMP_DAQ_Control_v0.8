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

        /// <summary>
        /// Flag indicating if the name has been manually customized by the user.
        /// If false, the name will be auto-generated based on signal parameters.
        /// </summary>
        public bool IsNameCustomized { get; set; }

        public SignalEvent()
        {
            EventId = Guid.NewGuid().ToString();
            Parameters = new Dictionary<string, double>();
            Color = "#4A90E2"; // Default blue
            IsNameCustomized = false; // Default: allow auto-naming
        }

        /// <summary>
        /// Gets the end time of this event
        /// </summary>
        public TimeSpan EndTime => StartTime + Duration;

        // Display properties for Events List table
        public string FrequencyDisplay
        {
            get
            {
                if (EventType == SignalEventType.Waveform && Parameters.ContainsKey("frequency"))
                    return $"{Parameters["frequency"]:F0}";
                return "N/A";
            }
        }

        public string AmplitudeDisplay
        {
            get
            {
                if (EventType == SignalEventType.Waveform && Parameters.ContainsKey("amplitude"))
                    return $"{Parameters["amplitude"]:F2}";
                return "N/A";
            }
        }

        public string OffsetDisplay
        {
            get
            {
                if (EventType == SignalEventType.Waveform && Parameters.ContainsKey("offset"))
                    return $"{Parameters["offset"]:F2}";
                return "N/A";
            }
        }

        public string VminDisplay
        {
            get
            {
                if (EventType == SignalEventType.Waveform && 
                    Parameters.ContainsKey("amplitude") && 
                    Parameters.ContainsKey("offset"))
                {
                    double vmin = Parameters["offset"] - Parameters["amplitude"];
                    return $"{vmin:F2}";
                }
                else if (EventType == SignalEventType.Ramp && Parameters.ContainsKey("startVoltage") && Parameters.ContainsKey("endVoltage"))
                {
                    double vmin = Math.Min(Parameters["startVoltage"], Parameters["endVoltage"]);
                    return $"{vmin:F2}";
                }
                else if (EventType == SignalEventType.DC && Parameters.ContainsKey("voltage"))
                {
                    return $"{Parameters["voltage"]:F2}";
                }
                return "N/A";
            }
        }

        public string VmaxDisplay
        {
            get
            {
                if (EventType == SignalEventType.Waveform && 
                    Parameters.ContainsKey("amplitude") && 
                    Parameters.ContainsKey("offset"))
                {
                    double vmax = Parameters["offset"] + Parameters["amplitude"];
                    return $"{vmax:F2}";
                }
                else if (EventType == SignalEventType.Ramp && Parameters.ContainsKey("startVoltage") && Parameters.ContainsKey("endVoltage"))
                {
                    double vmax = Math.Max(Parameters["startVoltage"], Parameters["endVoltage"]);
                    return $"{vmax:F2}";
                }
                else if (EventType == SignalEventType.DC && Parameters.ContainsKey("voltage"))
                {
                    return $"{Parameters["voltage"]:F2}";
                }
                return "N/A";
            }
        }

        public override string ToString()
        {
            return $"{Name} @ {StartTime.TotalSeconds:F1}s ({EventType})";
        }

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
                    
                    // CRITICAL: Validate amplitude + offset doesn't exceed 10V for analog devices
                    if (DeviceType == DeviceType.Analog)
                    {
                        double amplitude = Parameters["amplitude"];
                        double offset = Parameters.ContainsKey("offset") ? Parameters["offset"] : 0.0;
                        
                        if (amplitude < 0 || amplitude > 10)
                        {
                            errorMessage = "Amplitude must be 0-10V for analog devices.";
                            return false;
                        }
                        
                        if (offset < 0 || offset > 10)
                        {
                            errorMessage = "Offset must be 0-10V for analog devices.";
                            return false;
                        }
                        
                        // Check that peak voltage (offset + amplitude) doesn't exceed 10V
                        if (offset + amplitude > 10.0)
                        {
                            errorMessage = $"Peak voltage (offset {offset:F2}V + amplitude {amplitude:F2}V = {offset + amplitude:F2}V) exceeds maximum 10V for analog devices.";
                            return false;
                        }
                        
                        // Check that trough voltage (offset - amplitude) doesn't go below 0V
                        if (offset - amplitude < 0.0)
                        {
                            errorMessage = $"Trough voltage (offset {offset:F2}V - amplitude {amplitude:F2}V = {offset - amplitude:F2}V) is below minimum 0V for analog devices.";
                            return false;
                        }
                    }
                    break;
            }

            errorMessage = null;
            return true;
        }
    }
}

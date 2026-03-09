using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.Services
{
    /// <summary>
    /// Library of reusable signal templates
    /// </summary>
    public class SignalLibrary : ISignalLibrary
    {
        private readonly Dictionary<string, List<SignalEvent>> _signalsByCategory;
        private readonly object _lock = new object();

        public SignalLibrary()
        {
            _signalsByCategory = new Dictionary<string, List<SignalEvent>>();
            InitializeDefaultLibrary();
        }

        private void InitializeDefaultLibrary()
        {
            // ============================================
            // DIGITAL SIGNALS - PCI-1735U ONLY
            // Only HIGH (1) and LOW (0) states
            // Frequency controlled by grid timing
            // ============================================
            
            // Digital Write - Output states
            AddSignal(CreateDigitalState("HIGH", true, "Digital HIGH (1) state"), "Digital Write");
            AddSignal(CreateDigitalState("LOW", false, "Digital LOW (0) state"), "Digital Write");
            AddSignal(CreateDigitalPulse("Pulse 10ms", 10, "10ms HIGH pulse"), "Digital Write");
            AddSignal(CreateDigitalPulse("Pulse 50ms", 50, "50ms HIGH pulse"), "Digital Write");
            AddSignal(CreateDigitalPulse("Pulse 100ms", 100, "100ms HIGH pulse"), "Digital Write");
            AddSignal(CreateDigitalPulse("Pulse 500ms", 500, "500ms HIGH pulse"), "Digital Write");
            AddSignal(CreateDigitalPulse("Pulse 1s", 1000, "1 second HIGH pulse"), "Digital Write");

            // Digital Read - Input monitoring
            AddSignal(CreateDigitalRead("Read State", "Read digital input state"), "Digital Read");
            AddSignal(CreateDigitalTrigger("Wait HIGH", true, "Wait for HIGH transition"), "Digital Read");
            AddSignal(CreateDigitalTrigger("Wait LOW", false, "Wait for LOW transition"), "Digital Read");

            // ============================================
            // ANALOG SIGNALS - PCIe-1824 ONLY
            // DC voltages (0-10V), Ramps, Sinusoides
            // ============================================
            
            // Analog DC - Constant voltage outputs (0-10V)
            AddSignal(CreateAnalogDC("DC 0V", 0.0, "0V constant output"), "Analog DC");
            AddSignal(CreateAnalogDC("DC 1V", 1.0, "1V constant output"), "Analog DC");
            AddSignal(CreateAnalogDC("DC 3.3V", 3.3, "3.3V constant output"), "Analog DC");
            AddSignal(CreateAnalogDC("DC 5V", 5.0, "5V constant output"), "Analog DC");
            AddSignal(CreateAnalogDC("DC 10V", 10.0, "10V constant output"), "Analog DC");

            // Analog Ramps - Voltage ramps (0-30s duration, 0-10V range)
            AddSignal(CreateAnalogRamp("Ramp 0→5V (1s)", 0, 5, 1000, "0V to 5V in 1 second"), "Analog Ramps");
            AddSignal(CreateAnalogRamp("Ramp 0→10V (2s)", 0, 10, 2000, "0V to 10V in 2 seconds"), "Analog Ramps");
            AddSignal(CreateAnalogRamp("Ramp 5→0V (1s)", 5, 0, 1000, "5V to 0V in 1 second"), "Analog Ramps");
            AddSignal(CreateAnalogRamp("Ramp 10→0V (3s)", 10, 0, 3000, "10V to 0V in 3 seconds"), "Analog Ramps");
            AddSignal(CreateAnalogRamp("Ramp 0→10V (5s)", 0, 10, 5000, "0V to 10V in 5 seconds"), "Analog Ramps");
            AddSignal(CreateAnalogRamp("Ramp 0→10V (10s)", 0, 10, 10000, "0V to 10V in 10 seconds"), "Analog Ramps");

            // Analog Waveforms - Sinusoidal by LUT
            AddSignal(CreateAnalogWaveform("Sine 10Hz", 10, 5, 5, "10Hz sine, 5V amplitude, 5V offset"), "Analog Waveforms");
            AddSignal(CreateAnalogWaveform("Sine 50Hz", 50, 5, 5, "50Hz sine, 5V amplitude, 5V offset"), "Analog Waveforms");
            AddSignal(CreateAnalogWaveform("Sine 100Hz", 100, 5, 5, "100Hz sine, 5V amplitude, 5V offset"), "Analog Waveforms");
            AddSignal(CreateAnalogWaveform("Sine 500Hz", 500, 3, 5, "500Hz sine, 3V amplitude, 5V offset"), "Analog Waveforms");
            AddSignal(CreateAnalogWaveform("Sine 1kHz", 1000, 2, 5, "1kHz sine, 2V amplitude, 5V offset"), "Analog Waveforms");
        }

        /// <summary>
        /// Create Analog DC signal (0-10V constant voltage)
        /// ONLY for PCIe-1824 (Analog card)
        /// </summary>
        private SignalEvent CreateAnalogDC(string name, double voltage, string description)
        {
            return new SignalEvent
            {
                Name = name,
                EventType = SignalEventType.DC,
                DeviceType = DeviceType.Analog,
                Duration = TimeSpan.FromSeconds(1),
                Parameters = new Dictionary<string, double> { { "voltage", voltage } },
                Description = description,
                Color = "#3498DB"
            };
        }

        /// <summary>
        /// Create Analog Ramp signal (0-10V, 0-30s duration)
        /// ONLY for PCIe-1824 (Analog card)
        /// </summary>
        private SignalEvent CreateAnalogRamp(string name, double startV, double endV, int durationMs, string description)
        {
            return new SignalEvent
            {
                Name = name,
                EventType = SignalEventType.Ramp,
                DeviceType = DeviceType.Analog,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Parameters = new Dictionary<string, double>
                {
                    { "startVoltage", startV },
                    { "endVoltage", endV }
                },
                Description = description,
                Color = "#2ECC71"
            };
        }

        /// <summary>
        /// Create Analog Waveform (Sinusoidal by LUT)
        /// ONLY for PCIe-1824 (Analog card)
        /// </summary>
        private SignalEvent CreateAnalogWaveform(string name, double frequency, double amplitude, double offset, string description)
        {
            return new SignalEvent
            {
                Name = name,
                EventType = SignalEventType.Waveform,
                DeviceType = DeviceType.Analog,
                Duration = TimeSpan.FromSeconds(1),
                Parameters = new Dictionary<string, double>
                {
                    { "frequency", frequency },
                    { "amplitude", amplitude },
                    { "offset", offset }
                },
                Description = description,
                Color = "#9B59B6"
            };
        }

        /// <summary>
        /// Create Digital Pulse (HIGH for duration, then LOW)
        /// ONLY for PCI-1735U (Digital card)
        /// </summary>
        private SignalEvent CreateDigitalPulse(string name, int durationMs, string description)
        {
            return new SignalEvent
            {
                Name = name,
                EventType = SignalEventType.DigitalPulse,
                DeviceType = DeviceType.Digital,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Parameters = new Dictionary<string, double> { { "state", 1 } },
                Description = description,
                Color = "#E74C3C"
            };
        }

        /// <summary>
        /// Create Digital State (HIGH or LOW)
        /// ONLY for PCI-1735U (Digital card)
        /// </summary>
        private SignalEvent CreateDigitalState(string name, bool state, string description)
        {
            return new SignalEvent
            {
                Name = name,
                EventType = SignalEventType.DigitalState,
                DeviceType = DeviceType.Digital,
                Duration = TimeSpan.FromSeconds(1),
                Parameters = new Dictionary<string, double> { { "state", state ? 1 : 0 } },
                Description = description,
                Color = state ? "#27AE60" : "#7F8C8D"
            };
        }

        /// <summary>
        /// Create Digital Read operation
        /// ONLY for PCI-1735U (Digital card)
        /// </summary>
        private SignalEvent CreateDigitalRead(string name, string description)
        {
            return new SignalEvent
            {
                Name = name,
                EventType = SignalEventType.DigitalState,
                DeviceType = DeviceType.Digital,
                Duration = TimeSpan.FromMilliseconds(10),
                Parameters = new Dictionary<string, double> { { "read", 1 } },
                Description = description,
                Color = "#3498DB"
            };
        }

        /// <summary>
        /// Create Digital Trigger/Wait operation
        /// ONLY for PCI-1735U (Digital card)
        /// </summary>
        private SignalEvent CreateDigitalTrigger(string name, bool waitForHigh, string description)
        {
            return new SignalEvent
            {
                Name = name,
                EventType = SignalEventType.Wait,
                DeviceType = DeviceType.Digital,
                Duration = TimeSpan.FromMilliseconds(100),
                Parameters = new Dictionary<string, double> { { "trigger_state", waitForHigh ? 1 : 0 } },
                Description = description,
                Color = "#F39C12"
            };
        }

        public List<SignalEvent> GetAllSignals()
        {
            lock (_lock)
            {
                return _signalsByCategory.Values.SelectMany(list => list).ToList();
            }
        }

        public List<SignalEvent> GetSignalsByCategory(string category)
        {
            lock (_lock)
            {
                return _signalsByCategory.TryGetValue(category, out var signals)
                    ? new List<SignalEvent>(signals)
                    : new List<SignalEvent>();
            }
        }

        public SignalEvent GetSignal(string signalId)
        {
            lock (_lock)
            {
                return _signalsByCategory.Values
                    .SelectMany(list => list)
                    .FirstOrDefault(s => s.EventId == signalId);
            }
        }

        public void AddSignal(SignalEvent signal, string category)
        {
            if (signal == null)
                throw new ArgumentNullException(nameof(signal));

            if (string.IsNullOrWhiteSpace(category))
                category = "Custom";

            lock (_lock)
            {
                if (!_signalsByCategory.ContainsKey(category))
                {
                    _signalsByCategory[category] = new List<SignalEvent>();
                }

                _signalsByCategory[category].Add(signal);
            }
        }

        public bool RemoveSignal(string signalId)
        {
            lock (_lock)
            {
                foreach (var category in _signalsByCategory.Values)
                {
                    var signal = category.FirstOrDefault(s => s.EventId == signalId);
                    if (signal != null)
                    {
                        category.Remove(signal);
                        return true;
                    }
                }
                return false;
            }
        }

        public List<string> GetCategories()
        {
            lock (_lock)
            {
                return _signalsByCategory.Keys.ToList();
            }
        }

        public void SaveLibrary(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));

            try
            {
                var json = JsonConvert.SerializeObject(_signalsByCategory, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save library to {filePath}", ex);
            }
        }

        public void LoadLibrary(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Library file not found: {filePath}");

            try
            {
                var json = File.ReadAllText(filePath);
                var library = JsonConvert.DeserializeObject<Dictionary<string, List<SignalEvent>>>(json);

                lock (_lock)
                {
                    _signalsByCategory.Clear();
                    foreach (var kvp in library)
                    {
                        _signalsByCategory[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to load library from {filePath}", ex);
            }
        }

        public SignalEvent CreateFromTemplate(string signalId)
        {
            var template = GetSignal(signalId);
            if (template == null)
                throw new InvalidOperationException($"Signal template {signalId} not found.");

            // Create a deep copy
            var json = JsonConvert.SerializeObject(template);
            var instance = JsonConvert.DeserializeObject<SignalEvent>(json);

            // Assign new ID
            instance.EventId = Guid.NewGuid().ToString();

            return instance;
        }
    }
}

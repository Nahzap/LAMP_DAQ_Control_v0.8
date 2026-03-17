using System;
using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented
{
    /// <summary>
    /// Data Transfer Object for serializing/deserializing SignalTable
    /// </summary>
    public class SequenceDTO
    {
        public Guid SequenceId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public string Version { get; set; }
        public List<SignalEventDTO> Events { get; set; }
        
        public SequenceDTO()
        {
            Events = new List<SignalEventDTO>();
            Version = "1.0";
        }
    }
    
    /// <summary>
    /// DTO for individual signal events (DO to OO conversion for serialization)
    /// </summary>
    public class SignalEventDTO
    {
        public string EventId { get; set; }
        public string Name { get; set; }
        public long StartTimeNs { get; set; }
        public long DurationNs { get; set; }
        public int Channel { get; set; }
        public DeviceType DeviceType { get; set; }
        public string DeviceModel { get; set; }
        public SignalEventType EventType { get; set; }
        public string Color { get; set; }
        
        // Attributes
        public Dictionary<string, double> Parameters { get; set; }
        
        public SignalEventDTO()
        {
            Parameters = new Dictionary<string, double>();
        }
    }
}

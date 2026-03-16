using System;
using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented
{
    /// <summary>
    /// Manages multiple sequences using data-oriented architecture.
    /// Coordinates SignalTables and provides high-level operations.
    /// </summary>
    public class DataOrientedSequenceManager
    {
        private Dictionary<Guid, SequenceData> _sequences;
        
        public DataOrientedSequenceManager()
        {
            _sequences = new Dictionary<Guid, SequenceData>();
            System.Console.WriteLine("[DO MANAGER] Initialized DataOrientedSequenceManager");
        }
        
        /// <summary>
        /// Creates a new sequence
        /// </summary>
        public Guid CreateSequence(string name, string description = "")
        {
            var id = Guid.NewGuid();
            var data = new SequenceData
            {
                SequenceId = id,
                Name = name,
                Description = description,
                SignalTable = new SignalTable(64),
                Created = DateTime.Now,
                Modified = DateTime.Now
            };
            
            _sequences[id] = data;
            System.Console.WriteLine($"[DO MANAGER] Created sequence '{name}' (ID: {id})");
            return id;
        }
        
        /// <summary>
        /// Gets the signal table for a sequence
        /// </summary>
        public SignalTable GetSignalTable(Guid sequenceId)
        {
            return _sequences.TryGetValue(sequenceId, out var data) ? data.SignalTable : null;
        }
        
        /// <summary>
        /// Gets sequence metadata
        /// </summary>
        public SequenceMetadata GetMetadata(Guid sequenceId)
        {
            if (!_sequences.TryGetValue(sequenceId, out var data))
                return null;
            
            return new SequenceMetadata
            {
                SequenceId = data.SequenceId,
                Name = data.Name,
                Description = data.Description,
                EventCount = data.SignalTable.Count,
                Created = data.Created,
                Modified = data.Modified
            };
        }
        
        /// <summary>
        /// Adds a signal event (converts from OO to DO)
        /// </summary>
        public int AddSignal(Guid sequenceId, SignalEvent evt)
        {
            var table = GetSignalTable(sequenceId);
            if (table == null)
                throw new InvalidOperationException($"Sequence {sequenceId} not found");
            
            // CRITICAL: Parse EventId to preserve it when moving events
            Guid? eventId = null;
            if (!string.IsNullOrEmpty(evt.EventId) && Guid.TryParse(evt.EventId, out Guid parsedId))
            {
                eventId = parsedId;
            }
            
            // Convert TimeSpan to nanoseconds (1 tick = 100ns)
            int index = table.AddSignal(
                evt.Name,
                evt.StartTime.Ticks * 100,
                evt.Duration.Ticks * 100,
                evt.Channel,
                evt.DeviceType,
                evt.DeviceModel,
                evt.EventType,
                evt.Color,
                eventId // CRITICAL: Pass existing ID to preserve it
            );
            
            // Store type-specific attributes
            StoreAttributes(table, index, evt);
            
            _sequences[sequenceId].Modified = DateTime.Now;
            System.Console.WriteLine($"[DO MANAGER] Added signal '{evt.Name}' to sequence {sequenceId} at index {index}");
            return index;
        }
        
        /// <summary>
        /// Updates an existing signal
        /// </summary>
        public void UpdateSignal(Guid sequenceId, Guid eventId, SignalEvent updatedEvent)
        {
            var table = GetSignalTable(sequenceId);
            if (table == null)
                return;
            
            int index = table.FindIndex(eventId);
            if (index < 0)
            {
                System.Console.WriteLine($"[DO MANAGER] Event {eventId} not found in sequence {sequenceId}");
                return;
            }
            
            // CRITICAL: Update timing
            table.UpdateTiming(index, updatedEvent.StartTime.Ticks * 100, updatedEvent.Duration.Ticks * 100);
            
            // CRITICAL: Update channel and device (for drag & drop between channels)
            table.UpdateChannel(index, updatedEvent.Channel, updatedEvent.DeviceType, updatedEvent.DeviceModel);
            
            // Update attributes
            StoreAttributes(table, index, updatedEvent);
            
            _sequences[sequenceId].Modified = DateTime.Now;
            System.Console.WriteLine($"[DO MANAGER] Updated signal at index {index}");
        }
        
        /// <summary>
        /// Removes a signal event
        /// </summary>
        public void RemoveSignal(Guid sequenceId, Guid eventId)
        {
            var table = GetSignalTable(sequenceId);
            if (table == null)
                return;
            
            int index = table.FindIndex(eventId);
            if (index >= 0)
            {
                table.RemoveAt(index);
                _sequences[sequenceId].Modified = DateTime.Now;
                System.Console.WriteLine($"[DO MANAGER] Removed signal from sequence {sequenceId}");
            }
        }
        
        /// <summary>
        /// Detects conflicts in a sequence
        /// </summary>
        public List<(int indexA, int indexB)> DetectConflicts(Guid sequenceId)
        {
            var table = GetSignalTable(sequenceId);
            return table != null ? SignalOperations.DetectConflicts(table) : new List<(int, int)>();
        }
        
        /// <summary>
        /// Sorts sequence by start time
        /// </summary>
        public void SortSequence(Guid sequenceId)
        {
            var table = GetSignalTable(sequenceId);
            if (table != null)
            {
                SignalOperations.SortByStartTime(table);
                _sequences[sequenceId].Modified = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Validates a sequence
        /// </summary>
        public List<(int index, string error)> ValidateSequence(Guid sequenceId)
        {
            var table = GetSignalTable(sequenceId);
            return table != null ? SignalOperations.ValidateAll(table) : new List<(int, string)>();
        }
        
        /// <summary>
        /// Calculates total duration of a sequence
        /// </summary>
        public TimeSpan GetTotalDuration(Guid sequenceId)
        {
            var table = GetSignalTable(sequenceId);
            if (table == null)
                return TimeSpan.Zero;
            
            long durationNs = SignalOperations.CalculateTotalDuration(table);
            return TimeSpan.FromTicks(durationNs / 100);
        }
        
        /// <summary>
        /// Deletes a sequence
        /// </summary>
        public bool DeleteSequence(Guid sequenceId)
        {
            bool removed = _sequences.Remove(sequenceId);
            if (removed)
                System.Console.WriteLine($"[DO MANAGER] Deleted sequence {sequenceId}");
            return removed;
        }
        
        /// <summary>
        /// Gets all sequence IDs
        /// </summary>
        public List<Guid> GetAllSequenceIds()
        {
            return new List<Guid>(_sequences.Keys);
        }
        
        /// <summary>
        /// Stores attributes based on event type
        /// </summary>
        private void StoreAttributes(SignalTable table, int index, SignalEvent evt)
        {
            switch (evt.EventType)
            {
                case SignalEventType.Ramp:
                    if (evt.Parameters.TryGetValue("startVoltage", out double startV))
                        table.Attributes.SetStartVoltage(index, startV);
                    if (evt.Parameters.TryGetValue("endVoltage", out double endV))
                        table.Attributes.SetEndVoltage(index, endV);
                    break;
                
                case SignalEventType.DC:
                    if (evt.Parameters.TryGetValue("voltage", out double v))
                        table.Attributes.SetVoltage(index, v);
                    break;
                
                case SignalEventType.Waveform:
                    if (evt.Parameters.TryGetValue("frequency", out double f) &&
                        evt.Parameters.TryGetValue("amplitude", out double a) &&
                        evt.Parameters.TryGetValue("offset", out double o))
                    {
                        table.Attributes.SetWaveformParams(index, f, a, o);
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Internal sequence data container
        /// </summary>
        private class SequenceData
        {
            public Guid SequenceId { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public SignalTable SignalTable { get; set; }
            public DateTime Created { get; set; }
            public DateTime Modified { get; set; }
        }
    }
    
    /// <summary>
    /// Sequence metadata for UI display
    /// </summary>
    public class SequenceMetadata
    {
        public Guid SequenceId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int EventCount { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
    }
}

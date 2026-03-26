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
        public Guid CreateSequence(string name, string description, double desiredDurationSeconds = 10.0)
        {
            var id = Guid.NewGuid();
            var data = new SequenceData
            {
                SequenceId = id,
                Name = name,
                Description = description,
                SignalTable = new SignalTable(64),
                DesiredDurationSeconds = desiredDurationSeconds,
                Created = DateTime.Now,
                Modified = DateTime.Now
            };
            
            _sequences[id] = data;
            System.Console.WriteLine($"[DO MANAGER] Created sequence '{name}' (ID: {id}, MaxDuration: {desiredDurationSeconds}s)");
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
        /// Gets full sequence data (for loading)
        /// </summary>
        public SequenceInfo GetSequence(Guid sequenceId)
        {
            if (!_sequences.TryGetValue(sequenceId, out var data))
                return null;
            
            return new SequenceInfo
            {
                SequenceId = data.SequenceId,
                Name = data.Name,
                Description = data.Description,
                SignalTable = data.SignalTable,
                DurationSeconds = data.DesiredDurationSeconds, // Use CONFIGURED duration, not calculated
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
            
            // CRITICAL VALIDATION: Prevent corruption of signals by type mismatch
            // This protects analog signals from being overwritten by digital signals during drag & drop
            SignalEventType existingType = table.EventTypes[index];
            DeviceType existingDeviceType = table.DeviceTypes[index];
            string existingName = table.Names[index];
            
            if (existingType != updatedEvent.EventType)
            {
                System.Console.WriteLine($"[DO MANAGER ERROR] Cannot update '{existingName}' (EventId: {eventId}): EventType mismatch");
                System.Console.WriteLine($"[DO MANAGER ERROR]   Existing: {existingType}, Attempted: {updatedEvent.EventType}");
                System.Console.WriteLine($"[DO MANAGER ERROR]   This would corrupt the signal. Update REJECTED.");
                return;
            }
            
            if (existingDeviceType != updatedEvent.DeviceType)
            {
                System.Console.WriteLine($"[DO MANAGER ERROR] Cannot update '{existingName}' (EventId: {eventId}): DeviceType mismatch");
                System.Console.WriteLine($"[DO MANAGER ERROR]   Existing: {existingDeviceType}, Attempted: {updatedEvent.DeviceType}");
                System.Console.WriteLine($"[DO MANAGER ERROR]   This would corrupt the signal. Update REJECTED.");
                return;
            }
            
            System.Console.WriteLine($"[DO MANAGER] Validation passed for '{existingName}': {existingType} ({existingDeviceType})");
            
            // CRITICAL: Update timing
            table.UpdateTiming(index, updatedEvent.StartTime.Ticks * 100, updatedEvent.Duration.Ticks * 100);
            
            // CRITICAL: Update channel (only within same device type)
            table.UpdateChannel(index, updatedEvent.Channel, updatedEvent.DeviceType, updatedEvent.DeviceModel);
            
            // Update name and color
            table.UpdateNameAndColor(index, updatedEvent.Name, updatedEvent.Color);
            
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
            System.Console.WriteLine($"[DO MANAGER] RemoveSignal called - SequenceId: {sequenceId}, EventId: {eventId}");
            
            var table = GetSignalTable(sequenceId);
            if (table == null)
            {
                System.Console.WriteLine($"[DO MANAGER ERROR] Signal table not found for sequence {sequenceId}");
                return;
            }
            
            System.Console.WriteLine($"[DO MANAGER] Table Count before: {table.Count}");
            
            int index = table.FindIndex(eventId);
            System.Console.WriteLine($"[DO MANAGER] FindIndex returned: {index}");
            
            if (index >= 0)
            {
                System.Console.WriteLine($"[DO MANAGER] Removing event at index {index}: {table.Names[index]}");
                table.RemoveAt(index);
                _sequences[sequenceId].Modified = DateTime.Now;
                System.Console.WriteLine($"[DO MANAGER] Table Count after: {table.Count}");
                System.Console.WriteLine($"[DO MANAGER] Signal removed successfully from sequence {sequenceId}");
            }
            else
            {
                System.Console.WriteLine($"[DO MANAGER ERROR] Event {eventId} not found in table (FindIndex returned -1)");
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
        /// Calculates total duration of sequence
        /// </summary>
        public TimeSpan CalculateSequenceDuration(Guid sequenceId)
        {
            var table = GetSignalTable(sequenceId);
            if (table == null)
                return TimeSpan.Zero;
            
            long durationNs = SignalOperations.CalculateTotalDuration(table);
            return TimeSpan.FromTicks(durationNs / 100);
        }
        
        /// <summary>
        /// Saves sequence to file (JSON serialization)
        /// </summary>
        public void SaveSequence(Guid sequenceId, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));
            
            if (!_sequences.TryGetValue(sequenceId, out var data))
                throw new InvalidOperationException($"Sequence {sequenceId} not found.");
            
            try
            {
                // Convert SignalTable to DTO
                var dto = ConvertToDTO(data);
                
                // Serialize to JSON
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(dto, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(filePath, json);
                
                System.Console.WriteLine($"[DO MANAGER] Saved sequence '{data.Name}' to {filePath}");
            }
            catch (Exception ex)
            {
                throw new System.IO.IOException($"Failed to save sequence to {filePath}", ex);
            }
        }
        
        /// <summary>
        /// Loads sequence from file (JSON deserialization)
        /// </summary>
        public Guid LoadSequence(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));
            
            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException($"Sequence file not found: {filePath}");
            
            try
            {
                // Deserialize from JSON
                var json = System.IO.File.ReadAllText(filePath);
                var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<SequenceDTO>(json);
                
                if (dto == null)
                    throw new InvalidOperationException("Failed to deserialize sequence.");
                
                // Convert DTO to SequenceData
                var data = ConvertFromDTO(dto);
                
                // Register loaded sequence
                _sequences[data.SequenceId] = data;
                
                System.Console.WriteLine($"[DO MANAGER] Loaded sequence '{data.Name}' from {filePath}");
                return data.SequenceId;
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new InvalidOperationException($"Invalid sequence file format: {filePath}", ex);
            }
            catch (Exception ex)
            {
                throw new System.IO.IOException($"Failed to load sequence from {filePath}", ex);
            }
        }
        
        /// <summary>
        /// Converts SequenceData to DTO for serialization
        /// </summary>
        private SequenceDTO ConvertToDTO(SequenceData data)
        {
            var dto = new SequenceDTO
            {
                SequenceId = data.SequenceId,
                Name = data.Name,
                Description = data.Description,
                DesiredDurationSeconds = data.DesiredDurationSeconds, // Save configured max duration
                Created = data.Created,
                Modified = data.Modified
            };
            
            // Convert each event in SignalTable to DTO
            var table = data.SignalTable;
            for (int i = 0; i < table.Count; i++)
            {
                var eventDto = new SignalEventDTO
                {
                    EventId = table.EventIds[i].ToString(),
                    Name = table.Names[i],
                    StartTimeNs = table.StartTimesNs[i],
                    DurationNs = table.DurationsNs[i],
                    Channel = table.Channels[i],
                    DeviceType = table.DeviceTypes[i],
                    DeviceModel = table.DeviceModels[i],
                    EventType = table.EventTypes[i],
                    Color = table.Colors[i]
                };
                
                // Extract attributes based on event type
                switch (table.EventTypes[i])
                {
                    case SignalEventType.DC:
                        eventDto.Parameters["voltage"] = table.Attributes.GetVoltage(i);
                        break;
                    
                    case SignalEventType.Ramp:
                        eventDto.Parameters["startVoltage"] = table.Attributes.GetStartVoltage(i);
                        eventDto.Parameters["endVoltage"] = table.Attributes.GetEndVoltage(i);
                        break;
                    
                    case SignalEventType.Waveform:
                        var (freq, amp, offset) = table.Attributes.GetWaveformParams(i);
                        eventDto.Parameters["frequency"] = freq;
                        eventDto.Parameters["amplitude"] = amp;
                        eventDto.Parameters["offset"] = offset;
                        break;
                }
                
                dto.Events.Add(eventDto);
            }
            
            return dto;
        }
        
        /// <summary>
        /// Converts DTO to SequenceData for deserialization
        /// </summary>
        private SequenceData ConvertFromDTO(SequenceDTO dto)
        {
            var data = new SequenceData
            {
                SequenceId = dto.SequenceId,
                Name = dto.Name,
                Description = dto.Description,
                DesiredDurationSeconds = dto.DesiredDurationSeconds, // Restore configured max duration
                Created = dto.Created,
                Modified = dto.Modified,
                SignalTable = new SignalTable(Math.Max(64, dto.Events.Count * 2))
            };
            
            // Add each event to the SignalTable
            foreach (var eventDto in dto.Events)
            {
                Guid eventId = Guid.Parse(eventDto.EventId);
                
                int index = data.SignalTable.AddSignal(
                    eventDto.Name,
                    eventDto.StartTimeNs,
                    eventDto.DurationNs,
                    eventDto.Channel,
                    eventDto.DeviceType,
                    eventDto.DeviceModel,
                    eventDto.EventType,
                    eventDto.Color,
                    eventId
                );
                
                // Restore attributes based on event type
                switch (eventDto.EventType)
                {
                    case SignalEventType.DC:
                        if (eventDto.Parameters.TryGetValue("voltage", out double v))
                            data.SignalTable.Attributes.SetVoltage(index, v);
                        break;
                    
                    case SignalEventType.Ramp:
                        if (eventDto.Parameters.TryGetValue("startVoltage", out double sv))
                            data.SignalTable.Attributes.SetStartVoltage(index, sv);
                        if (eventDto.Parameters.TryGetValue("endVoltage", out double ev))
                            data.SignalTable.Attributes.SetEndVoltage(index, ev);
                        break;
                    
                    case SignalEventType.Waveform:
                        if (eventDto.Parameters.TryGetValue("frequency", out double f) &&
                            eventDto.Parameters.TryGetValue("amplitude", out double a) &&
                            eventDto.Parameters.TryGetValue("offset", out double o))
                        {
                            data.SignalTable.Attributes.SetWaveformParams(index, f, a, o);
                        }
                        break;
                }
            }
            
            return data;
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
                
                case SignalEventType.DigitalState:
                    // Store state as voltage (1.0 = HIGH, 0.0 = LOW)
                    if (evt.Parameters.TryGetValue("state", out double state))
                    {
                        table.Attributes.SetVoltage(index, state);
                    }
                    break;
                
                case SignalEventType.PulseTrain:
                    // Store PulseTrain params using waveform storage (frequency, dutyCycle, vHigh)
                    if (evt.Parameters.TryGetValue("frequency", out double freq) &&
                        evt.Parameters.TryGetValue("dutyCycle", out double duty) &&
                        evt.Parameters.TryGetValue("vHigh", out double vHigh))
                    {
                        table.Attributes.SetWaveformParams(index, freq, duty, vHigh);
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
            public double DesiredDurationSeconds { get; set; } // CONFIGURED max duration, not calculated from events
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
    
    /// <summary>
    /// Full sequence information (for loading)
    /// </summary>
    public class SequenceInfo
    {
        public Guid SequenceId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public SignalTable SignalTable { get; set; }
        public double DurationSeconds { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.Services
{
    /// <summary>
    /// Engine for managing signal sequences
    /// </summary>
    public class SequenceEngine : ISequenceEngine
    {
        private readonly Dictionary<string, SignalSequence> _sequences;
        private readonly object _lock = new object();

        public SequenceEngine()
        {
            _sequences = new Dictionary<string, SignalSequence>();
        }

        public SignalSequence CreateSequence(string name, string description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Sequence name cannot be empty.", nameof(name));

            var sequence = new SignalSequence
            {
                Name = name,
                Description = description ?? string.Empty,
                Created = DateTime.Now,
                Modified = DateTime.Now,
                Author = Environment.UserName
            };

            lock (_lock)
            {
                _sequences[sequence.SequenceId] = sequence;
            }

            return sequence;
        }

        public SignalSequence GetSequence(string sequenceId)
        {
            if (string.IsNullOrWhiteSpace(sequenceId))
                throw new ArgumentException("Sequence ID cannot be empty.", nameof(sequenceId));

            lock (_lock)
            {
                return _sequences.TryGetValue(sequenceId, out var sequence) ? sequence : null;
            }
        }

        public List<SignalSequence> GetAllSequences()
        {
            lock (_lock)
            {
                return _sequences.Values.ToList();
            }
        }

        public SignalEvent GetEvent(string sequenceId, string eventId)
        {
            var sequence = GetSequence(sequenceId);
            if (sequence == null) return null;

            lock (_lock)
            {
                return sequence.Events.FirstOrDefault(e => e.EventId == eventId);
            }
        }

        public List<SignalEvent> GetAllEvents(string sequenceId)
        {
            var sequence = GetSequence(sequenceId);
            if (sequence == null) return new List<SignalEvent>();

            lock (_lock)
            {
                return sequence.Events.OrderBy(e => e.StartTime).ToList();
            }
        }

        public void AddEvent(string sequenceId, SignalEvent evt)
        {
            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            var sequence = GetSequence(sequenceId);
            if (sequence == null)
                throw new InvalidOperationException($"Sequence {sequenceId} not found.");

            System.Console.WriteLine($"[SEQ ENGINE] AddEvent '{evt.Name}': StartTime={evt.StartTime.TotalSeconds:F6}s, Duration={evt.Duration.TotalSeconds:F6}s");

            lock (_lock)
            {
                sequence.AddEvent(evt);
            }
        }

        public bool RemoveEvent(string sequenceId, string eventId)
        {
            var sequence = GetSequence(sequenceId);
            if (sequence == null)
                return false;

            lock (_lock)
            {
                return sequence.RemoveEvent(eventId);
            }
        }

        public bool UpdateEvent(string sequenceId, SignalEvent evt)
        {
            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            var sequence = GetSequence(sequenceId);
            if (sequence == null)
                return false;

            lock (_lock)
            {
                var existing = sequence.Events.FirstOrDefault(e => e.EventId == evt.EventId);
                if (existing == null)
                    return false;

                System.Console.WriteLine($"[SEQ ENGINE] UpdateEvent '{evt.Name}': OLD StartTime={existing.StartTime.TotalSeconds:F6}s, NEW StartTime={evt.StartTime.TotalSeconds:F6}s");

                // Update properties
                existing.Name = evt.Name;
                existing.StartTime = evt.StartTime;
                existing.Duration = evt.Duration;
                existing.Channel = evt.Channel;
                existing.DeviceType = evt.DeviceType;
                existing.EventType = evt.EventType;
                existing.Parameters = new Dictionary<string, double>(evt.Parameters);
                existing.Description = evt.Description;
                existing.Color = evt.Color;

                sequence.Modified = DateTime.Now;
                return true;
            }
        }

        public bool ValidateSequence(string sequenceId, out List<string> errors)
        {
            var sequence = GetSequence(sequenceId);
            if (sequence == null)
            {
                errors = new List<string> { "Sequence not found." };
                return false;
            }

            return sequence.Validate(out errors);
        }

        public void SaveSequence(string sequenceId, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));

            var sequence = GetSequence(sequenceId);
            if (sequence == null)
                throw new InvalidOperationException($"Sequence {sequenceId} not found.");

            try
            {
                var json = JsonConvert.SerializeObject(sequence, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save sequence to {filePath}", ex);
            }
        }

        public SignalSequence LoadSequence(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Sequence file not found: {filePath}");

            try
            {
                var json = File.ReadAllText(filePath);
                var sequence = JsonConvert.DeserializeObject<SignalSequence>(json);

                if (sequence == null)
                    throw new InvalidOperationException("Failed to deserialize sequence.");

                // Register loaded sequence
                lock (_lock)
                {
                    _sequences[sequence.SequenceId] = sequence;
                }

                return sequence;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid sequence file format: {filePath}", ex);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to load sequence from {filePath}", ex);
            }
        }

        public bool DeleteSequence(string sequenceId)
        {
            lock (_lock)
            {
                return _sequences.Remove(sequenceId);
            }
        }

        public SignalSequence DuplicateSequence(string sequenceId, string newName)
        {
            var original = GetSequence(sequenceId);
            if (original == null)
                throw new InvalidOperationException($"Sequence {sequenceId} not found.");

            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New sequence name cannot be empty.", nameof(newName));

            // Create a deep copy
            var json = JsonConvert.SerializeObject(original);
            var duplicate = JsonConvert.DeserializeObject<SignalSequence>(json);

            // Update metadata
            duplicate.SequenceId = Guid.NewGuid().ToString();
            duplicate.Name = newName;
            duplicate.Created = DateTime.Now;
            duplicate.Modified = DateTime.Now;

            // Generate new IDs for all events
            foreach (var evt in duplicate.Events)
            {
                evt.EventId = Guid.NewGuid().ToString();
            }

            lock (_lock)
            {
                _sequences[duplicate.SequenceId] = duplicate;
            }

            return duplicate;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented
{
    /// <summary>
    /// Adapter to expose SignalTable to MVVM ViewModels.
    /// Converts between data-oriented (index-based) and object-oriented (SignalEvent) representations.
    /// </summary>
    public class SignalTableAdapter
    {
        private SignalTable _table;
        private DataOrientedSequenceManager _manager;
        private Guid _sequenceId;
        
        public SignalTableAdapter(DataOrientedSequenceManager manager, Guid sequenceId)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _sequenceId = sequenceId;
            _table = manager.GetSignalTable(sequenceId);
            
            if (_table == null)
                throw new InvalidOperationException($"Sequence {sequenceId} not found");
            
            System.Console.WriteLine($"[ADAPTER] Created adapter for sequence {sequenceId}");
        }
        
        /// <summary>
        /// Gets the underlying signal table
        /// </summary>
        public SignalTable Table => _table;
        
        /// <summary>
        /// Converts index to SignalEvent for WPF binding
        /// </summary>
        public SignalEvent GetEvent(int index)
        {
            if (index < 0 || index >= _table.Count)
                return null;
            
            var evt = new SignalEvent
            {
                EventId = _table.EventIds[index].ToString(),
                Name = _table.Names[index],
                StartTime = TimeSpan.FromTicks(_table.StartTimesNs[index] / 100),
                Duration = TimeSpan.FromTicks(_table.DurationsNs[index] / 100),
                Channel = _table.Channels[index],
                DeviceType = _table.DeviceTypes[index],
                DeviceModel = _table.DeviceModels[index],
                EventType = _table.EventTypes[index],
                Color = _table.Colors[index],
                Parameters = new Dictionary<string, double>()
            };
            
            // Load type-specific attributes
            LoadAttributes(evt, index);
            
            return evt;
        }
        
        /// <summary>
        /// Gets all events as a list
        /// </summary>
        public List<SignalEvent> GetAllEvents()
        {
            var events = new List<SignalEvent>(_table.Count);
            for (int i = 0; i < _table.Count; i++)
            {
                events.Add(GetEvent(i));
            }
            return events;
        }
        
        /// <summary>
        /// Creates an ObservableCollection facade for WPF binding
        /// </summary>
        public ObservableCollection<SignalEvent> AsObservableCollection()
        {
            var collection = new ObservableCollection<SignalEvent>();
            for (int i = 0; i < _table.Count; i++)
            {
                collection.Add(GetEvent(i));
            }
            
            System.Console.WriteLine($"[ADAPTER] Created ObservableCollection with {collection.Count} events");
            return collection;
        }
        
        /// <summary>
        /// Gets events for a specific channel
        /// </summary>
        public List<SignalEvent> GetEventsForChannel(int channel, DeviceType deviceType, string deviceModel)
        {
            var indices = SignalOperations.FilterByChannel(_table, channel, deviceType, deviceModel);
            var events = new List<SignalEvent>(indices.Length);
            
            foreach (int idx in indices)
            {
                events.Add(GetEvent(idx));
            }
            
            return events;
        }
        
        /// <summary>
        /// Adds an event (returns index)
        /// </summary>
        public int AddEvent(SignalEvent evt)
        {
            return _manager.AddSignal(_sequenceId, evt);
        }
        
        /// <summary>
        /// Updates an existing event
        /// </summary>
        public void UpdateEvent(string eventId, SignalEvent updatedEvent)
        {
            if (Guid.TryParse(eventId, out Guid guid))
            {
                _manager.UpdateSignal(_sequenceId, guid, updatedEvent);
            }
        }
        
        /// <summary>
        /// Removes an event
        /// </summary>
        public void RemoveEvent(string eventId)
        {
            System.Console.WriteLine($"[ADAPTER] RemoveEvent called with EventId: {eventId}");
            
            if (!Guid.TryParse(eventId, out Guid guid))
            {
                System.Console.WriteLine($"[ADAPTER ERROR] Failed to parse EventId as Guid: {eventId}");
                return;
            }
            
            System.Console.WriteLine($"[ADAPTER] Parsed Guid: {guid}");
            System.Console.WriteLine($"[ADAPTER] Count before removal: {_table.Count}");
            
            _manager.RemoveSignal(_sequenceId, guid);
            
            System.Console.WriteLine($"[ADAPTER] Count after removal: {_table.Count}");
        }
        
        /// <summary>
        /// Detects conflicts
        /// </summary>
        public List<(SignalEvent eventA, SignalEvent eventB)> DetectConflicts()
        {
            var conflicts = _manager.DetectConflicts(_sequenceId);
            var result = new List<(SignalEvent, SignalEvent)>(conflicts.Count);
            
            foreach (var (idxA, idxB) in conflicts)
            {
                result.Add((GetEvent(idxA), GetEvent(idxB)));
            }
            
            return result;
        }
        
        /// <summary>
        /// Sorts events by start time
        /// </summary>
        public void SortByStartTime()
        {
            _manager.SortSequence(_sequenceId);
        }
        
        /// <summary>
        /// Validates all events
        /// </summary>
        public List<(SignalEvent evt, string error)> ValidateAll()
        {
            var errors = _manager.ValidateSequence(_sequenceId);
            var result = new List<(SignalEvent, string)>(errors.Count);
            
            foreach (var (index, error) in errors)
            {
                result.Add((GetEvent(index), error));
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets total duration
        /// </summary>
        public TimeSpan GetTotalDuration()
        {
            return _manager.GetTotalDuration(_sequenceId);
        }
        
        /// <summary>
        /// Gets event count
        /// </summary>
        public int Count => _table.Count;
        
        /// <summary>
        /// Loads attributes from table into SignalEvent
        /// </summary>
        private void LoadAttributes(SignalEvent evt, int index)
        {
            switch (evt.EventType)
            {
                case SignalEventType.Ramp:
                    evt.Parameters["startVoltage"] = _table.Attributes.GetStartVoltage(index, 0);
                    evt.Parameters["endVoltage"] = _table.Attributes.GetEndVoltage(index, 0);
                    break;
                
                case SignalEventType.DC:
                    evt.Parameters["voltage"] = _table.Attributes.GetVoltage(index, 0);
                    break;
                
                case SignalEventType.Waveform:
                    var (f, a, o) = _table.Attributes.GetWaveformParams(index);
                    evt.Parameters["frequency"] = f;
                    evt.Parameters["amplitude"] = a;
                    evt.Parameters["offset"] = o;
                    break;
            }
        }
        
        /// <summary>
        /// Clears all events
        /// </summary>
        public void Clear()
        {
            _table.Clear();
        }
        
        /// <summary>
        /// Finds event by ID
        /// </summary>
        public SignalEvent FindEventById(string eventId)
        {
            if (!Guid.TryParse(eventId, out Guid guid))
                return null;
            
            int index = _table.FindIndex(guid);
            return index >= 0 ? GetEvent(index) : null;
        }
    }
}

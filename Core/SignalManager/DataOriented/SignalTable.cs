using System;
using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented
{
    /// <summary>
    /// Column-oriented data structure for signal events.
    /// Optimized for cache-friendly iteration and SIMD operations.
    /// Similar to a DataFrame or SQL table.
    /// </summary>
    public class SignalTable
    {
        // === IDENTIFICATION ===
        public Guid[] EventIds { get; private set; }
        
        // === TIMING (nanoseconds for precision) ===
        public long[] StartTimesNs { get; private set; }
        public long[] DurationsNs { get; private set; }
        
        // === ROUTING ===
        public int[] Channels { get; private set; }
        public DeviceType[] DeviceTypes { get; private set; }
        public string[] DeviceModels { get; private set; }
        
        // === METADATA ===
        public string[] Names { get; private set; }
        public SignalEventType[] EventTypes { get; private set; }
        public string[] Colors { get; private set; }
        
        // === OPTIONAL ATTRIBUTES (sparse storage) ===
        public SignalAttributeStore Attributes { get; private set; }
        
        // === STATE ===
        public int Count { get; private set; }
        public int Capacity { get; private set; }
        
        // O(1) lookup index
        private Dictionary<Guid, int> _idToIndex;
        
        public SignalTable(int initialCapacity = 64)
        {
            Capacity = initialCapacity;
            Count = 0;
            
            // Preallocate contiguous arrays
            EventIds = new Guid[Capacity];
            StartTimesNs = new long[Capacity];
            DurationsNs = new long[Capacity];
            Channels = new int[Capacity];
            DeviceTypes = new DeviceType[Capacity];
            DeviceModels = new string[Capacity];
            Names = new string[Capacity];
            EventTypes = new SignalEventType[Capacity];
            Colors = new string[Capacity];
            
            Attributes = new SignalAttributeStore(Capacity);
            _idToIndex = new Dictionary<Guid, int>(Capacity);
            
            System.Console.WriteLine($"[SIGNAL TABLE] Initialized with capacity {Capacity}");
        }
        
        /// <summary>
        /// Adds a signal event to the table
        /// </summary>
        public int AddSignal(
            string name,
            long startTimeNs,
            long durationNs,
            int channel,
            DeviceType deviceType,
            string deviceModel,
            SignalEventType eventType,
            string color = "#2ECC71",
            Guid? eventId = null)
        {
            // Auto-resize if needed
            if (Count >= Capacity)
            {
                Resize(Capacity * 2);
            }
            
            int index = Count;
            Guid id = eventId ?? Guid.NewGuid(); // CRITICAL: Use provided ID or generate new
            
            // Insert into column vectors
            EventIds[index] = id;
            StartTimesNs[index] = startTimeNs;
            DurationsNs[index] = durationNs;
            Channels[index] = channel;
            DeviceTypes[index] = deviceType;
            DeviceModels[index] = deviceModel;
            Names[index] = name;
            EventTypes[index] = eventType;
            Colors[index] = color;
            
            // Update lookup index
            _idToIndex[id] = index;
            Count++;
            
            System.Console.WriteLine($"[SIGNAL TABLE] Added '{name}' at index {index} (Count={Count})");
            return index;
        }
        
        /// <summary>
        /// Removes event at index using swap-with-last technique (O(1))
        /// Common in Entity Component Systems
        /// </summary>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            int lastIndex = Count - 1;
            Guid removedId = EventIds[index];
            
            System.Console.WriteLine($"[SIGNAL TABLE] Removing '{Names[index]}' at index {index}");
            System.Console.WriteLine($"[SIGNAL TABLE]   RemovedId: {removedId}");
            System.Console.WriteLine($"[SIGNAL TABLE]   LastIndex: {lastIndex}, LastId: {EventIds[lastIndex]}, LastName: '{Names[lastIndex]}'");
            
            // Debug: Print _idToIndex state BEFORE removal
            System.Console.WriteLine($"[SIGNAL TABLE] _idToIndex state BEFORE removal:");
            for (int i = 0; i < Count; i++)
            {
                int dictIndex = _idToIndex.TryGetValue(EventIds[i], out int idx) ? idx : -999;
                System.Console.WriteLine($"[SIGNAL TABLE]   [{i}] {Names[i]} | EventId: {EventIds[i]} | Dict maps to: {dictIndex} {(dictIndex == i ? "✓" : "❌ MISMATCH")}");
            }
            
            if (index != lastIndex)
            {
                Guid movedId = EventIds[lastIndex];
                string movedName = Names[lastIndex];
                
                System.Console.WriteLine($"[SIGNAL TABLE] Swapping: Moving '{movedName}' (Id: {movedId}) from index {lastIndex} → {index}");
                
                // Swap with last element
                EventIds[index] = EventIds[lastIndex];
                StartTimesNs[index] = StartTimesNs[lastIndex];
                DurationsNs[index] = DurationsNs[lastIndex];
                Channels[index] = Channels[lastIndex];
                DeviceTypes[index] = DeviceTypes[lastIndex];
                DeviceModels[index] = DeviceModels[lastIndex];
                Names[index] = Names[lastIndex];
                EventTypes[index] = EventTypes[lastIndex];
                Colors[index] = Colors[lastIndex];
                
                // Transfer attributes
                Attributes.Swap(index, lastIndex);
            }
            
            // Clear last slot
            Attributes.Clear(lastIndex);
            Count--;
            
            // CRITICAL FIX: Rebuild entire _idToIndex dictionary to ensure synchronization
            // This fixes the bug where dictionary indices become stale after swap operations
            System.Console.WriteLine($"[SIGNAL TABLE] Rebuilding _idToIndex dictionary for {Count} events...");
            _idToIndex.Clear();
            for (int i = 0; i < Count; i++)
            {
                _idToIndex[EventIds[i]] = i;
            }
            System.Console.WriteLine($"[SIGNAL TABLE] Dictionary rebuilt successfully");
            
            // Debug: Print _idToIndex state AFTER removal
            System.Console.WriteLine($"[SIGNAL TABLE] _idToIndex state AFTER removal (Count={Count}):");
            for (int i = 0; i < Count; i++)
            {
                int dictIndex = _idToIndex.TryGetValue(EventIds[i], out int idx) ? idx : -999;
                System.Console.WriteLine($"[SIGNAL TABLE]   [{i}] {Names[i]} | EventId: {EventIds[i]} | Dict maps to: {dictIndex} {(dictIndex == i ? "✓" : "❌ MISMATCH")}");
            }
            
            System.Console.WriteLine($"[SIGNAL TABLE] Removed successfully (Count={Count})");
        }
        
        /// <summary>
        /// Finds index by EventId in O(1)
        /// </summary>
        public int FindIndex(Guid eventId)
        {
            return _idToIndex.TryGetValue(eventId, out int index) ? index : -1;
        }
        
        /// <summary>
        /// Gets time range (start, end) in nanoseconds
        /// </summary>
        public (long start, long end) GetTimeRange(int index)
        {
            return (StartTimesNs[index], StartTimesNs[index] + DurationsNs[index]);
        }
        
        /// <summary>
        /// Updates timing for event at index
        /// </summary>
        public void UpdateTiming(int index, long newStartNs, long newDurationNs)
        {
            if (index < 0 || index >= Count)
                return;
            
            StartTimesNs[index] = newStartNs;
            DurationsNs[index] = newDurationNs;
            
            System.Console.WriteLine($"[SIGNAL TABLE] Updated timing for '{Names[index]}': {newStartNs / 1e9:F3}s, duration {newDurationNs / 1e9:F3}s");
        }
        
        /// <summary>
        /// Updates channel and device info for event at index (for moving between channels)
        /// </summary>
        public void UpdateChannel(int index, int newChannel, DeviceType newDeviceType, string newDeviceModel)
        {
            if (index < 0 || index >= Count)
                return;
            
            Channels[index] = newChannel;
            DeviceTypes[index] = newDeviceType;
            DeviceModels[index] = newDeviceModel;
            
            System.Console.WriteLine($"[SIGNAL TABLE] Updated channel for '{Names[index]}': CH{newChannel}, Device={newDeviceModel} ({newDeviceType})");
        }
        
        /// <summary>
        /// Updates name and color for event at index
        /// </summary>
        public void UpdateNameAndColor(int index, string newName, string newColor)
        {
            if (index < 0 || index >= Count)
                return;
            
            Names[index] = newName;
            Colors[index] = newColor;
            
            System.Console.WriteLine($"[SIGNAL TABLE] Updated name to '{newName}' at index {index}");
        }
        
        /// <summary>
        /// Clears all events
        /// </summary>
        public void Clear()
        {
            Count = 0;
            _idToIndex.Clear();
            System.Console.WriteLine("[SIGNAL TABLE] Cleared all events");
        }
        
        private void Resize(int newCapacity)
        {
            System.Console.WriteLine($"[SIGNAL TABLE] Resizing from {Capacity} to {newCapacity}");
            
            var newEventIds = new Guid[newCapacity];
            var newStartTimesNs = new long[newCapacity];
            var newDurationsNs = new long[newCapacity];
            var newChannels = new int[newCapacity];
            var newDeviceTypes = new DeviceType[newCapacity];
            var newDeviceModels = new string[newCapacity];
            var newNames = new string[newCapacity];
            var newEventTypes = new SignalEventType[newCapacity];
            var newColors = new string[newCapacity];
            
            Array.Copy(EventIds, newEventIds, Count);
            Array.Copy(StartTimesNs, newStartTimesNs, Count);
            Array.Copy(DurationsNs, newDurationsNs, Count);
            Array.Copy(Channels, newChannels, Count);
            Array.Copy(DeviceTypes, newDeviceTypes, Count);
            Array.Copy(DeviceModels, newDeviceModels, Count);
            Array.Copy(Names, newNames, Count);
            Array.Copy(EventTypes, newEventTypes, Count);
            Array.Copy(Colors, newColors, Count);
            
            EventIds = newEventIds;
            StartTimesNs = newStartTimesNs;
            DurationsNs = newDurationsNs;
            Channels = newChannels;
            DeviceTypes = newDeviceTypes;
            DeviceModels = newDeviceModels;
            Names = newNames;
            EventTypes = newEventTypes;
            Colors = newColors;
            
            Attributes.Resize(newCapacity);
            Capacity = newCapacity;
        }
        
        /// <summary>
        /// Gets a debug string for an event at index
        /// </summary>
        public string GetEventDebugString(int index)
        {
            if (index < 0 || index >= Count)
                return "Invalid index";
            
            double startSec = StartTimesNs[index] / 1e9;
            double durSec = DurationsNs[index] / 1e9;
            return $"{Names[index]} ({EventTypes[index]}): {startSec:F3}s + {durSec:F3}s on {DeviceModels[index]} CH{Channels[index]}";
        }
    }
}

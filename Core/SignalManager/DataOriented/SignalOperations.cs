using System;
using System.Collections.Generic;
using System.Linq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented
{
    /// <summary>
    /// Stateless operations on SignalTable.
    /// Pure functions with no side effects.
    /// </summary>
    public static class SignalOperations
    {
        /// <summary>
        /// Detects all time-overlap conflicts in a table
        /// </summary>
        public static List<(int indexA, int indexB)> DetectConflicts(SignalTable table)
        {
            var conflicts = new List<(int, int)>();
            
            // Group by (channel, deviceType, deviceModel)
            var groups = new Dictionary<(int channel, DeviceType type, string model), List<int>>();
            
            for (int i = 0; i < table.Count; i++)
            {
                var key = (table.Channels[i], table.DeviceTypes[i], table.DeviceModels[i]);
                if (!groups.ContainsKey(key))
                    groups[key] = new List<int>();
                groups[key].Add(i);
            }
            
            // Detect overlaps within each group
            foreach (var group in groups.Values)
            {
                // Sort by start time
                group.Sort((a, b) => table.StartTimesNs[a].CompareTo(table.StartTimesNs[b]));
                
                for (int i = 0; i < group.Count - 1; i++)
                {
                    int idxA = group[i];
                    int idxB = group[i + 1];
                    
                    long endA = table.StartTimesNs[idxA] + table.DurationsNs[idxA];
                    long startB = table.StartTimesNs[idxB];
                    
                    // Tolerance: 1ms = 1,000,000 ns
                    const long tolerance = 1_000_000;
                    
                    if (endA > startB + tolerance)
                    {
                        conflicts.Add((idxA, idxB));
                        System.Console.WriteLine($"[CONFLICT] '{table.Names[idxA]}' overlaps with '{table.Names[idxB]}'");
                    }
                }
            }
            
            System.Console.WriteLine($"[SIGNAL OPS] DetectConflicts: Found {conflicts.Count} conflicts");
            return conflicts;
        }
        
        /// <summary>
        /// Sorts table by start time (in-place)
        /// </summary>
        public static void SortByStartTime(SignalTable table)
        {
            if (table.Count <= 1)
                return;
            
            // Create array of indices
            var indices = Enumerable.Range(0, table.Count).ToArray();
            
            // Sort indices by StartTimesNs
            Array.Sort(indices, (a, b) => table.StartTimesNs[a].CompareTo(table.StartTimesNs[b]));
            
            // Apply permutation
            ApplyPermutation(table, indices);
            
            System.Console.WriteLine($"[SIGNAL OPS] SortByStartTime: Sorted {table.Count} events");
        }
        
        /// <summary>
        /// Filters events by channel
        /// </summary>
        public static int[] FilterByChannel(SignalTable table, int channel, DeviceType deviceType, string deviceModel)
        {
            var result = new List<int>();
            
            for (int i = 0; i < table.Count; i++)
            {
                if (table.Channels[i] == channel &&
                    table.DeviceTypes[i] == deviceType &&
                    table.DeviceModels[i] == deviceModel)
                {
                    result.Add(i);
                }
            }
            
            System.Console.WriteLine($"[SIGNAL OPS] FilterByChannel: Found {result.Count} events on {deviceModel} CH{channel}");
            return result.ToArray();
        }
        
        /// <summary>
        /// Validates all events in table
        /// </summary>
        public static List<(int index, string error)> ValidateAll(SignalTable table)
        {
            var errors = new List<(int, string)>();
            
            for (int i = 0; i < table.Count; i++)
            {
                // Validate timing
                if (table.StartTimesNs[i] < 0)
                    errors.Add((i, "Start time cannot be negative"));
                
                if (table.DurationsNs[i] <= 0)
                    errors.Add((i, "Duration must be positive"));
                
                // Validate channel
                if (table.Channels[i] < 0 || table.Channels[i] > 31)
                    errors.Add((i, "Channel must be 0-31"));
                
                // Validate parameters by type
                switch (table.EventTypes[i])
                {
                    case SignalEventType.Ramp:
                        double startV = table.Attributes.GetStartVoltage(i, double.NaN);
                        double endV = table.Attributes.GetEndVoltage(i, double.NaN);
                        
                        if (double.IsNaN(startV))
                            errors.Add((i, "Ramp requires startVoltage parameter"));
                        else if (startV < 0 || startV > 10)
                            errors.Add((i, "startVoltage must be 0-10V"));
                        
                        if (double.IsNaN(endV))
                            errors.Add((i, "Ramp requires endVoltage parameter"));
                        else if (endV < 0 || endV > 10)
                            errors.Add((i, "endVoltage must be 0-10V"));
                        break;
                    
                    case SignalEventType.DC:
                        double voltage = table.Attributes.GetVoltage(i, double.NaN);
                        if (double.IsNaN(voltage))
                            errors.Add((i, "DC requires voltage parameter"));
                        else if (voltage < 0 || voltage > 10)
                            errors.Add((i, "voltage must be 0-10V"));
                        break;
                    
                    case SignalEventType.Waveform:
                        var (freq, amp, offset) = table.Attributes.GetWaveformParams(i);
                        if (freq <= 0)
                            errors.Add((i, "frequency must be > 0"));
                        if (amp < 0 || amp > 10)
                            errors.Add((i, "amplitude must be 0-10V"));
                        if (offset < 0 || offset > 10)
                            errors.Add((i, "offset must be 0-10V"));
                        if (amp + offset > 10)
                            errors.Add((i, "amplitude + offset must be ≤ 10V"));
                        break;
                }
            }
            
            System.Console.WriteLine($"[SIGNAL OPS] ValidateAll: Found {errors.Count} errors");
            return errors;
        }
        
        /// <summary>
        /// Calculates total duration of sequence (max end time)
        /// </summary>
        public static long CalculateTotalDuration(SignalTable table)
        {
            long maxEndTime = 0;
            
            for (int i = 0; i < table.Count; i++)
            {
                long endTime = table.StartTimesNs[i] + table.DurationsNs[i];
                if (endTime > maxEndTime)
                    maxEndTime = endTime;
            }
            
            return maxEndTime;
        }
        
        /// <summary>
        /// Applies a permutation to table (cycle-following algorithm)
        /// CRITICAL: Also permutes SignalAttributeStore to keep indices synchronized
        /// </summary>
        private static void ApplyPermutation(SignalTable table, int[] perm)
        {
            var visited = new bool[table.Count];
            
            for (int i = 0; i < table.Count; i++)
            {
                if (visited[i] || perm[i] == i)
                    continue;
                
                int j = i;
                
                // Save initial element (ALL data including attributes)
                var tempId = table.EventIds[i];
                var tempStart = table.StartTimesNs[i];
                var tempDuration = table.DurationsNs[i];
                var tempChannel = table.Channels[i];
                var tempDeviceType = table.DeviceTypes[i];
                var tempDeviceModel = table.DeviceModels[i];
                var tempName = table.Names[i];
                var tempEventType = table.EventTypes[i];
                var tempColor = table.Colors[i];
                
                // CRITICAL: Also save attributes from SignalAttributeStore
                var (tempFreq, tempAmp, tempOffset) = table.Attributes.GetWaveformParams(i);
                var tempStartV = table.Attributes.GetStartVoltage(i, double.NaN);
                var tempEndV = table.Attributes.GetEndVoltage(i, double.NaN);
                var tempVoltage = table.Attributes.GetVoltage(i, double.NaN);
                
                // Follow cycle
                while (perm[j] != i)
                {
                    int next = perm[j];
                    
                    // Copy from next to j (ALL data including attributes)
                    table.EventIds[j] = table.EventIds[next];
                    table.StartTimesNs[j] = table.StartTimesNs[next];
                    table.DurationsNs[j] = table.DurationsNs[next];
                    table.Channels[j] = table.Channels[next];
                    table.DeviceTypes[j] = table.DeviceTypes[next];
                    table.DeviceModels[j] = table.DeviceModels[next];
                    table.Names[j] = table.Names[next];
                    table.EventTypes[j] = table.EventTypes[next];
                    table.Colors[j] = table.Colors[next];
                    
                    // CRITICAL: Also copy attributes
                    table.Attributes.Swap(j, next);
                    
                    visited[j] = true;
                    j = next;
                }
                
                // Place initial element in last position of cycle
                table.EventIds[j] = tempId;
                table.StartTimesNs[j] = tempStart;
                table.DurationsNs[j] = tempDuration;
                table.Channels[j] = tempChannel;
                table.DeviceTypes[j] = tempDeviceType;
                table.DeviceModels[j] = tempDeviceModel;
                table.Names[j] = tempName;
                table.EventTypes[j] = tempEventType;
                table.Colors[j] = tempColor;
                
                // CRITICAL: Restore attributes for initial element
                if (!double.IsNaN(tempFreq))
                    table.Attributes.SetWaveformParams(j, tempFreq, tempAmp, tempOffset);
                if (!double.IsNaN(tempStartV))
                    table.Attributes.SetStartVoltage(j, tempStartV);
                if (!double.IsNaN(tempEndV))
                    table.Attributes.SetEndVoltage(j, tempEndV);
                if (!double.IsNaN(tempVoltage))
                    table.Attributes.SetVoltage(j, tempVoltage);
                
                visited[j] = true;
            }
        }
    }
}

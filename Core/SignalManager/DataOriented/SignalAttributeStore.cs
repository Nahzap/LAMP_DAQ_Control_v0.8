using System;
using System.Collections.Generic;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented
{
    /// <summary>
    /// Sparse storage for optional signal attributes by event type.
    /// Uses dictionaries to avoid wasting memory on unused attributes.
    /// </summary>
    public class SignalAttributeStore
    {
        // Ramp attributes
        private Dictionary<int, double> _startVoltages;
        private Dictionary<int, double> _endVoltages;
        
        // DC attributes
        private Dictionary<int, double> _voltages;
        
        // Waveform attributes
        private Dictionary<int, double> _frequencies;
        private Dictionary<int, double> _amplitudes;
        private Dictionary<int, double> _offsets;
        
        public SignalAttributeStore(int capacity)
        {
            int sparseCapacity = capacity / 4;
            _startVoltages = new Dictionary<int, double>(sparseCapacity);
            _endVoltages = new Dictionary<int, double>(sparseCapacity);
            _voltages = new Dictionary<int, double>(sparseCapacity);
            _frequencies = new Dictionary<int, double>(sparseCapacity);
            _amplitudes = new Dictionary<int, double>(sparseCapacity);
            _offsets = new Dictionary<int, double>(sparseCapacity);
        }
        
        // === RAMP ATTRIBUTES ===
        
        public void SetStartVoltage(int index, double voltage)
        {
            _startVoltages[index] = voltage;
        }
        
        public double GetStartVoltage(int index, double defaultValue = 0)
        {
            return _startVoltages.TryGetValue(index, out double val) ? val : defaultValue;
        }
        
        public void SetEndVoltage(int index, double voltage)
        {
            _endVoltages[index] = voltage;
        }
        
        public double GetEndVoltage(int index, double defaultValue = 0)
        {
            return _endVoltages.TryGetValue(index, out double val) ? val : defaultValue;
        }
        
        // === DC ATTRIBUTES ===
        
        public void SetVoltage(int index, double voltage)
        {
            _voltages[index] = voltage;
        }
        
        public double GetVoltage(int index, double defaultValue = 0)
        {
            return _voltages.TryGetValue(index, out double val) ? val : defaultValue;
        }
        
        // === WAVEFORM ATTRIBUTES ===
        
        public void SetWaveformParams(int index, double freq, double amp, double offset)
        {
            _frequencies[index] = freq;
            _amplitudes[index] = amp;
            _offsets[index] = offset;
        }
        
        public (double freq, double amp, double offset) GetWaveformParams(int index)
        {
            double freq = _frequencies.TryGetValue(index, out double f) ? f : 0;
            double amp = _amplitudes.TryGetValue(index, out double a) ? a : 0;
            double offset = _offsets.TryGetValue(index, out double o) ? o : 0;
            return (freq, amp, offset);
        }
        
        // === UTILITY ===
        
        /// <summary>
        /// Swaps attributes between two indices (for swap-based removal)
        /// </summary>
        public void Swap(int indexA, int indexB)
        {
            SwapInDict(_startVoltages, indexA, indexB);
            SwapInDict(_endVoltages, indexA, indexB);
            SwapInDict(_voltages, indexA, indexB);
            SwapInDict(_frequencies, indexA, indexB);
            SwapInDict(_amplitudes, indexA, indexB);
            SwapInDict(_offsets, indexA, indexB);
        }
        
        private void SwapInDict(Dictionary<int, double> dict, int a, int b)
        {
            bool hasA = dict.TryGetValue(a, out double valA);
            bool hasB = dict.TryGetValue(b, out double valB);
            
            dict.Remove(a);
            dict.Remove(b);
            
            if (hasA) dict[b] = valA;
            if (hasB) dict[a] = valB;
        }
        
        public void Resize(int newCapacity)
        {
            // Dictionaries auto-resize, no action needed
        }
        
        /// <summary>
        /// Removes all attributes for an index
        /// </summary>
        public void Clear(int index)
        {
            _startVoltages.Remove(index);
            _endVoltages.Remove(index);
            _voltages.Remove(index);
            _frequencies.Remove(index);
            _amplitudes.Remove(index);
            _offsets.Remove(index);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace LAMP_DAQ_Control_v0_8.UI.Services
{
    /// <summary>
    /// Rastrea comandos enviados a salidas analógicas para visualización
    /// Ya que la PCIE-1824 solo tiene salidas (AO), no podemos leer los valores físicos.
    /// Este servicio mantiene un historial de los comandos enviados para graficarlos.
    /// </summary>
    public class AnalogOutputTracker
    {
        private readonly Dictionary<int, CircularBuffer<DataPoint>> _channelBuffers;
        private readonly int _maxPoints;
        
        public event EventHandler<AnalogDataEventArgs> DataRecorded;
        
        public AnalogOutputTracker(int maxPointsPerChannel = 1000)
        {
            _channelBuffers = new Dictionary<int, CircularBuffer<DataPoint>>();
            _maxPoints = maxPointsPerChannel;
        }
        
        /// <summary>
        /// Registra un comando de escritura de voltaje
        /// </summary>
        public void RecordWrite(int channel, double voltage, DateTime timestamp)
        {
            if (!_channelBuffers.ContainsKey(channel))
            {
                _channelBuffers[channel] = new CircularBuffer<DataPoint>(_maxPoints);
            }
            
            var dataPoint = new DataPoint
            {
                Timestamp = timestamp,
                Value = voltage
            };
            
            _channelBuffers[channel].Add(dataPoint);
            
            // Emitir evento para actualización en tiempo real
            DataRecorded?.Invoke(this, new AnalogDataEventArgs
            {
                Channel = channel,
                Voltage = voltage,
                Timestamp = timestamp
            });
        }
        
        /// <summary>
        /// Obtiene el historial de un canal específico
        /// </summary>
        public DataPoint[] GetChannelHistory(int channel)
        {
            return _channelBuffers.ContainsKey(channel)
                ? _channelBuffers[channel].ToArray()
                : Array.Empty<DataPoint>();
        }
        
        /// <summary>
        /// Obtiene el último valor registrado en un canal
        /// </summary>
        public DataPoint? GetLastValue(int channel)
        {
            if (_channelBuffers.ContainsKey(channel))
            {
                var buffer = _channelBuffers[channel].ToArray();
                return buffer.Length > 0 ? buffer[buffer.Length - 1] : (DataPoint?)null;
            }
            return null;
        }
        
        /// <summary>
        /// Limpia el historial de un canal
        /// </summary>
        public void ClearHistory(int channel)
        {
            if (_channelBuffers.ContainsKey(channel))
            {
                _channelBuffers[channel].Clear();
            }
        }
        
        /// <summary>
        /// Limpia el historial de todos los canales
        /// </summary>
        public void ClearAllHistory()
        {
            foreach (var buffer in _channelBuffers.Values)
            {
                buffer.Clear();
            }
        }
        
        /// <summary>
        /// Obtiene lista de canales que tienen historial
        /// </summary>
        public int[] GetActiveChannels()
        {
            return _channelBuffers.Keys.ToArray();
        }
    }
    
    /// <summary>
    /// Punto de datos con timestamp
    /// </summary>
    public struct DataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }
    
    /// <summary>
    /// Argumentos del evento de datos analógicos
    /// </summary>
    public class AnalogDataEventArgs : EventArgs
    {
        public int Channel { get; set; }
        public double Voltage { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Buffer circular para almacenar datos con tamaño limitado
    /// </summary>
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _count;
        
        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be positive", nameof(capacity));
                
            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }
        
        public void Add(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }
        
        public T[] ToArray()
        {
            T[] result = new T[_count];
            for (int i = 0; i < _count; i++)
            {
                int index = (_head - _count + i + _buffer.Length) % _buffer.Length;
                result[i] = _buffer[index];
            }
            return result;
        }
        
        public void Clear()
        {
            _count = 0;
            _head = 0;
        }
        
        public int Count => _count;
        public int Capacity => _buffer.Length;
    }
}

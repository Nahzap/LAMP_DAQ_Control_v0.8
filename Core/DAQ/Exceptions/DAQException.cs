using System;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Exceptions
{
    /// <summary>
    /// Base exception for all DAQ-related exceptions
    /// </summary>
    [Serializable]
    public class DAQException : Exception
    {
        public DAQException() { }
        public DAQException(string message) : base(message) { }
        public DAQException(string message, Exception inner) : base(message, inner) { }
        protected DAQException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

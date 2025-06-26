using System;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Exceptions
{
    /// <summary>
    /// Exception thrown when a DAQ device fails to initialize
    /// </summary>
    [Serializable]
    public class DAQInitializationException : DAQException
    {
        public DAQInitializationException() { }
        public DAQInitializationException(string message) : base(message) { }
        public DAQInitializationException(string message, Exception inner) : base(message, inner) { }
        protected DAQInitializationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

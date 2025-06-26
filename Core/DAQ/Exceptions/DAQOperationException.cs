using System;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Exceptions
{
    /// <summary>
    /// Exception thrown when a DAQ operation fails
    /// </summary>
    [Serializable]
    public class DAQOperationException : DAQException
    {
        public DAQOperationException() { }
        public DAQOperationException(string message) : base(message) { }
        public DAQOperationException(string message, Exception inner) : base(message, inner) { }
        protected DAQOperationException(
          System.Runtime.Serialization.StreamingContext context,
          System.Runtime.Serialization.SerializationInfo info) : base(info, context) { }
    }
}

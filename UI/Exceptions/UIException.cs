using System;

namespace LAMP_DAQ_Control_v0_8.UI.Exceptions
{
    /// <summary>
    /// Excepción base para errores relacionados con la interfaz de usuario
    /// </summary>
    public class UIException : Exception
    {
        public UIException() : base() { }
        
        public UIException(string message) : base(message) { }
        
        public UIException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    /// <summary>
    /// Excepción para errores de entrada de usuario
    /// </summary>
    public class UserInputException : UIException
    {
        public UserInputException() : base() { }
        
        public UserInputException(string message) : base(message) { }
        
        public UserInputException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    /// <summary>
    /// Excepción para errores de menú
    /// </summary>
    public class MenuException : UIException
    {
        public MenuException() : base() { }
        
        public MenuException(string message) : base(message) { }
        
        public MenuException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    /// <summary>
    /// Excepción para errores de detección de dispositivos
    /// </summary>
    public class DeviceDetectionException : UIException
    {
        public DeviceDetectionException() : base() { }
        
        public DeviceDetectionException(string message) : base(message) { }
        
        public DeviceDetectionException(string message, Exception innerException) : base(message, innerException) { }
    }
}

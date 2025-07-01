namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Models
{
    /// <summary>
    /// Enum para identificar el tipo de dispositivo DAQ
    /// </summary>
    public enum DeviceType
    {
        /// <summary>
        /// Tipo de dispositivo desconocido
        /// </summary>
        Unknown,
        
        /// <summary>
        /// Dispositivo analógico (PCIe-1824)
        /// </summary>
        Analog,
        
        /// <summary>
        /// Dispositivo digital (PCI-1735U)
        /// </summary>
        Digital
    }
}

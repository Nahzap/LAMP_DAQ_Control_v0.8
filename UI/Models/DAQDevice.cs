using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;

namespace LAMP_DAQ_Control_v0_8.UI.Models
{
    /// <summary>
    /// Representa un dispositivo DAQ detectado en el sistema
    /// </summary>
    public class DAQDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ConfigFile { get; set; }
        public bool IsConnected { get; set; }
        public int DeviceNumber { get; set; } = -1;
        public DeviceType DeviceType { get; set; } = DeviceType.Unknown;
        public int BoardId { get; set; } = -1;
        
        /// <summary>
        /// Determina el tipo de dispositivo basado en el Board ID
        /// </summary>
        /// <param name="boardId">ID de la tarjeta configurado por switches físicos</param>
        /// <returns>Tipo de dispositivo (Digital, Analógico o Desconocido)</returns>
        public static DeviceType GetDeviceTypeFromBoardId(int boardId)
        {
            // Según la configuración de los switches físicos:
            // Board ID 0 y 1 son para dispositivos digitales (PCI-1735U)
            // Board ID 2 y superiores son para dispositivos analógicos (PCIe-1824)
            if (boardId >= 0 && boardId <= 1)
            {
                return DeviceType.Digital;
            }
            else if (boardId >= 2)
            {
                return DeviceType.Analog;
            }
            
            return DeviceType.Unknown;
        }
    }
}

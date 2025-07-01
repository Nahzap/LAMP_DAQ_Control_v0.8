using System.Threading.Tasks;
using LAMP_DAQ_Control_v0_8.UI.Models;

namespace LAMP_DAQ_Control_v0_8.UI.Interfaces
{
    /// <summary>
    /// Interfaz para manejadores de menú específicos de dispositivos
    /// </summary>
    public interface IDeviceMenuHandler
    {
        Task HandleDeviceMenu(DAQDevice device);
    }
}

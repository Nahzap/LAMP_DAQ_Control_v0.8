using System.Threading.Tasks;

namespace LAMP_DAQ_Control_v0_8.UI.Interfaces
{
    /// <summary>
    /// Interfaz base para manejadores de menú
    /// </summary>
    public interface IMenuHandler
    {
        Task HandleMenu();
    }
}

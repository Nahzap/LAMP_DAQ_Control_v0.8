using System.Collections.Generic;

namespace LAMP_DAQ_Control_v0_8.UI.Interfaces
{
    /// <summary>
    /// Interfaz para servicios de consola
    /// </summary>
    public interface IConsoleService
    {
        void ShowMessage(string message);
        void ShowError(string error);
        int GetIntInput(string prompt, int min, int max);
        double GetDoubleInput(string prompt, double min, double max);
        int ShowMenu(string title, IEnumerable<string> options);
    }
}

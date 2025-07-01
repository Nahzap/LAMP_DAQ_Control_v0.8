using System;
using System.Collections.Generic;
using System.Linq;
using LAMP_DAQ_Control_v0_8.UI.Interfaces;

namespace LAMP_DAQ_Control_v0_8.UI.Services
{
    /// <summary>
    /// Servicio para operaciones de entrada/salida en consola
    /// </summary>
    public class ConsoleService : IConsoleService
    {
        public void ShowMessage(string message)
        {
            Console.WriteLine(message);
        }
        
        public void ShowError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ResetColor();
        }
        
        public int ShowMenu(string title, IEnumerable<string> options)
        {
            Console.Clear();
            Console.WriteLine($"=== {title} ===\n");
            
            var optionsList = options.ToList();
            for (int i = 0; i < optionsList.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {optionsList[i]}");
            }
            
            return GetIntInput("\nSeleccione una opción: ", 1, optionsList.Count);
        }
        
        public int GetIntInput(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int value) && value >= min && value <= max)
                {
                    return value;
                }
                ShowError($"Por favor ingrese un número entre {min} y {max}.");
            }
        }
        
        public double GetDoubleInput(string prompt, double min, double max)
        {
            while (true)
            {
                Console.Write(prompt);
                if (double.TryParse(Console.ReadLine(), out double value) && value >= min && value <= max)
                {
                    return value;
                }
                ShowError($"Por favor ingrese un número entre {min} y {max}.");
            }
        }
    }
}

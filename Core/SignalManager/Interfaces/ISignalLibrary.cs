using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.Interfaces
{
    /// <summary>
    /// Interface for signal library operations
    /// </summary>
    public interface ISignalLibrary
    {
        /// <summary>
        /// Gets all available signal templates
        /// </summary>
        List<SignalEvent> GetAllSignals();

        /// <summary>
        /// Gets signals by category
        /// </summary>
        List<SignalEvent> GetSignalsByCategory(string category);

        /// <summary>
        /// Gets a signal by ID
        /// </summary>
        SignalEvent GetSignal(string signalId);

        /// <summary>
        /// Adds a custom signal to library
        /// </summary>
        void AddSignal(SignalEvent signal, string category);

        /// <summary>
        /// Removes a signal from library
        /// </summary>
        bool RemoveSignal(string signalId);

        /// <summary>
        /// Gets all categories
        /// </summary>
        List<string> GetCategories();

        /// <summary>
        /// Saves library to file
        /// </summary>
        void SaveLibrary(string filePath);

        /// <summary>
        /// Loads library from file
        /// </summary>
        void LoadLibrary(string filePath);

        /// <summary>
        /// Creates a signal instance from template
        /// </summary>
        SignalEvent CreateFromTemplate(string signalId);
    }
}

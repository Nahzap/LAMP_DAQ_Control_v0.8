using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.Interfaces
{
    /// <summary>
    /// Interface for sequence engine operations
    /// </summary>
    public interface ISequenceEngine
    {
        /// <summary>
        /// Creates a new empty sequence
        /// </summary>
        SignalSequence CreateSequence(string name, string description = null);

        /// <summary>
        /// Gets a sequence by ID
        /// </summary>
        SignalSequence GetSequence(string sequenceId);

        /// <summary>
        /// Gets all sequences
        /// </summary>
        List<SignalSequence> GetAllSequences();

        /// <summary>
        /// Gets a specific event by ID from a sequence
        /// </summary>
        SignalEvent GetEvent(string sequenceId, string eventId);

        /// <summary>
        /// Gets all events from a sequence, sorted by start time
        /// </summary>
        List<SignalEvent> GetAllEvents(string sequenceId);

        /// <summary>
        /// Adds an event to a sequence
        /// </summary>
        void AddEvent(string sequenceId, SignalEvent evt);

        /// <summary>
        /// Removes an event from a sequence
        /// </summary>
        bool RemoveEvent(string sequenceId, string eventId);

        /// <summary>
        /// Updates an existing event
        /// </summary>
        bool UpdateEvent(string sequenceId, SignalEvent evt);

        /// <summary>
        /// Validates a sequence
        /// </summary>
        bool ValidateSequence(string sequenceId, out List<string> errors);

        /// <summary>
        /// Saves a sequence to file
        /// </summary>
        void SaveSequence(string sequenceId, string filePath);

        /// <summary>
        /// Loads a sequence from file
        /// </summary>
        SignalSequence LoadSequence(string filePath);

        /// <summary>
        /// Deletes a sequence
        /// </summary>
        bool DeleteSequence(string sequenceId);

        /// <summary>
        /// Duplicates a sequence
        /// </summary>
        SignalSequence DuplicateSequence(string sequenceId, string newName);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.Interfaces
{
    /// <summary>
    /// Interface for execution engine operations
    /// </summary>
    public interface IExecutionEngine
    {
        /// <summary>
        /// Executes a sequence asynchronously
        /// </summary>
        Task ExecuteSequenceAsync(SignalSequence sequence, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses execution
        /// </summary>
        void Pause();

        /// <summary>
        /// Resumes execution
        /// </summary>
        void Resume();

        /// <summary>
        /// Stops execution
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets current execution state
        /// </summary>
        ExecutionState State { get; }

        /// <summary>
        /// Gets current time in sequence
        /// </summary>
        TimeSpan CurrentTime { get; }

        /// <summary>
        /// Event fired when execution state changes
        /// </summary>
        event EventHandler<ExecutionStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Event fired when an event is executed
        /// </summary>
        event EventHandler<EventExecutedEventArgs> EventExecuted;

        /// <summary>
        /// Event fired on execution error
        /// </summary>
        event EventHandler<ExecutionErrorEventArgs> ExecutionError;
    }

    /// <summary>
    /// Execution states
    /// </summary>
    public enum ExecutionState
    {
        Idle,
        Running,
        Paused,
        Stopping,
        Completed,
        Error
    }

    /// <summary>
    /// Event args for state changes
    /// </summary>
    public class ExecutionStateChangedEventArgs : EventArgs
    {
        public ExecutionState OldState { get; set; }
        public ExecutionState NewState { get; set; }
    }

    /// <summary>
    /// Event args for event execution
    /// </summary>
    public class EventExecutedEventArgs : EventArgs
    {
        public SignalEvent Event { get; set; }
        public TimeSpan ActualTime { get; set; }
    }

    /// <summary>
    /// Event args for execution errors
    /// </summary>
    public class ExecutionErrorEventArgs : EventArgs
    {
        public SignalEvent Event { get; set; }
        public Exception Error { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.Services
{
    /// <summary>
    /// Engine for executing signal sequences with precise timing
    /// </summary>
    public class ExecutionEngine : IExecutionEngine
    {
        private readonly Dictionary<string, DAQController> _deviceControllers;
        private ExecutionState _state;
        private TimeSpan _currentTime;
        private Stopwatch _executionTimer;
        private System.Threading.Timer _playheadUpdateTimer;
        private CancellationTokenSource _cts;
        private ManualResetEventSlim _pauseEvent;
        private SignalSequence _currentSequence;
        private bool _isLoopEnabled;

        public ExecutionEngine(Dictionary<string, DAQController> deviceControllers)
        {
            _deviceControllers = deviceControllers ?? throw new ArgumentNullException(nameof(deviceControllers));
            if (_deviceControllers.Count == 0)
                throw new ArgumentException("At least one device controller is required", nameof(deviceControllers));
            
            System.Console.WriteLine($"[EXEC ENGINE INIT] Initialized with {_deviceControllers.Count} device controller(s):");
            foreach (var kvp in _deviceControllers)
            {
                System.Console.WriteLine($"[EXEC ENGINE INIT]   - {kvp.Key}: {kvp.Value.DeviceModel}");
            }
            
            _state = ExecutionState.Idle;
            _pauseEvent = new ManualResetEventSlim(true);
        }

        public ExecutionState State
        {
            get { lock (this) { return _state; } }
            private set
            {
                ExecutionState oldState;
                lock (this)
                {
                    if (_state == value) return;
                    oldState = _state;
                    _state = value;
                }
                OnStateChanged(new ExecutionStateChangedEventArgs { OldState = oldState, NewState = value });
            }
        }

        public TimeSpan CurrentTime
        {
            get { lock (this) { return _currentTime; } }
            private set 
            { 
                TimeSpan oldValue;
                lock (this) 
                { 
                    oldValue = _currentTime;
                    _currentTime = value; 
                }
                // Fire PropertyChanged-like event for UI updates
                if (oldValue != value)
                {
                    OnEventExecuted(new EventExecutedEventArgs 
                    { 
                        Event = null, 
                        ActualTime = value 
                    });
                }
            }
        }

        public bool IsLoopEnabled
        {
            get { lock (this) { return _isLoopEnabled; } }
            set 
            { 
                lock (this) 
                { 
                    _isLoopEnabled = value;
                    System.Console.WriteLine($"[EXEC ENGINE] Loop control set to: {value}");
                } 
            }
        }

        public event EventHandler<ExecutionStateChangedEventArgs> StateChanged;
        public event EventHandler<EventExecutedEventArgs> EventExecuted;
        public event EventHandler<ExecutionErrorEventArgs> ExecutionError;

        public async Task ExecuteSequenceAsync(SignalSequence sequence, CancellationToken cancellationToken = default)
        {
            System.Console.WriteLine($"[EXEC ENGINE] ExecuteSequenceAsync called for sequence: {sequence?.Name}");
            
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));

            if (State != ExecutionState.Idle)
            {
                System.Console.WriteLine($"[EXEC ENGINE ERROR] Cannot start - State is {State}, not Idle");
                throw new InvalidOperationException("Execution already in progress.");
            }

            // Validate sequence
            System.Console.WriteLine($"[EXEC ENGINE] Validating sequence...");
            if (!sequence.Validate(out var errors))
            {
                System.Console.WriteLine($"[EXEC ENGINE ERROR] Validation failed: {string.Join(", ", errors)}");
                throw new InvalidOperationException($"Sequence validation failed: {string.Join(", ", errors)}");
            }
            System.Console.WriteLine($"[EXEC ENGINE] Validation passed");

            _currentSequence = sequence;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executionTimer = Stopwatch.StartNew();
            State = ExecutionState.Running;
            CurrentTime = TimeSpan.Zero;
            
            System.Console.WriteLine($"[EXEC ENGINE] Starting execution timer");

            // NUEVO: Start continuous playhead update timer (30ms = ~33 FPS)
            _playheadUpdateTimer = new System.Threading.Timer(UpdatePlayheadCallback, null, 0, 30);
            System.Console.WriteLine($"[EXEC ENGINE] Playhead update timer started (30ms interval)");

            try
            {
                // Sort events by start time
                var sortedEvents = sequence.GetEventsSorted().ToList();
                System.Console.WriteLine($"[EXEC ENGINE] Found {sortedEvents.Count} events to execute");

                // Group events by start time for parallel execution
                var eventGroups = GroupEventsByStartTime(sortedEvents);
                System.Console.WriteLine($"[EXEC ENGINE] Grouped into {eventGroups.Count} time slots for parallel dispatch");

                foreach (var group in eventGroups)
                {
                    // Check for cancellation
                    if (_cts.Token.IsCancellationRequested)
                    {
                        System.Console.WriteLine($"[EXEC ENGINE] Cancellation requested, stopping");
                        State = ExecutionState.Stopping;
                        break;
                    }

                    // Wait for pause
                    _pauseEvent.Wait(_cts.Token);

                    // Wait until group start time
                    var groupTime = group.Key;
                    System.Console.WriteLine($"[EXEC ENGINE] Waiting until {groupTime.TotalSeconds:F2}s ({group.Value.Count} concurrent events)...");
                    await WaitUntilAsync(groupTime, _cts.Token);
                    System.Console.WriteLine($"[EXEC ENGINE] Time {groupTime.TotalSeconds:F2}s reached, dispatching {group.Value.Count} events in parallel...");

                    if (group.Value.Count == 1)
                    {
                        // Single event — execute directly (no Task overhead)
                        var evt = group.Value[0];
                        try
                        {
                            await ExecuteEventAsync(evt, _cts.Token);
                            System.Console.WriteLine($"[EXEC ENGINE] Event executed: {evt.Name}");
                            OnEventExecuted(new EventExecutedEventArgs { Event = evt, ActualTime = CurrentTime });
                        }
                        catch (Exception ex)
                        {
                            OnExecutionError(new ExecutionErrorEventArgs { Event = evt, Error = ex });
                            if (ex is OperationCanceledException) throw;
                        }
                    }
                    else
                    {
                        // PARALLEL DISPATCH: Multiple events at same start time
                        // Execute on different devices simultaneously
                        var tasks = new List<Task>(group.Value.Count);
                        foreach (var evt in group.Value)
                        {
                            System.Console.WriteLine($"[EXEC ENGINE] Parallel dispatch: {evt.Name} (Device: {evt.DeviceModel}, Type: {evt.EventType})");
                            tasks.Add(ExecuteEventWithErrorHandling(evt, _cts.Token));
                        }

                        // Wait for ALL concurrent events to complete
                        await Task.WhenAll(tasks);
                        System.Console.WriteLine($"[EXEC ENGINE] All {group.Value.Count} parallel events completed at t={groupTime.TotalSeconds:F2}s");
                    }
                }

                System.Console.WriteLine($"[EXEC ENGINE] All events executed successfully");
                State = ExecutionState.Completed;
                
                // Check if loop is enabled for auto-restart
                bool shouldLoop = IsLoopEnabled;
                System.Console.WriteLine($"[EXEC ENGINE] Loop enabled: {shouldLoop}");
                
                if (shouldLoop && !_cts.Token.IsCancellationRequested)
                {
                    System.Console.WriteLine($"[EXEC ENGINE] Loop enabled - restarting sequence '{_currentSequence.Name}'");
                    await Task.Delay(100); // Brief delay
                    
                    // CRITICAL: Reset state to Idle FIRST to allow re-execution
                    State = ExecutionState.Idle;
                    System.Console.WriteLine($"[EXEC ENGINE] State reset to Idle for loop restart");
                    
                    // Store sequence reference before cleanup
                    var sequenceToLoop = _currentSequence;
                    
                    // Re-execute the sequence
                    await ExecuteSequenceAsync(sequenceToLoop, cancellationToken);
                    return; // Exit here to avoid final cleanup
                }
                else
                {
                    // No loop - reset to Idle after completion
                    await Task.Delay(100); // Brief delay
                    State = ExecutionState.Idle;
                    System.Console.WriteLine($"[EXEC ENGINE] Sequence completed - State reset to Idle");
                }
            }
            catch (OperationCanceledException ex)
            {
                System.Console.WriteLine($"[EXEC ENGINE] Execution cancelled: {ex.Message}");
                State = ExecutionState.Idle;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[EXEC ENGINE ERROR] Execution failed: {ex.Message}");
                System.Console.WriteLine($"[EXEC ENGINE ERROR] Stack trace: {ex.StackTrace}");
                State = ExecutionState.Error;
                throw;
            }
            finally
            {
                // Stop playhead update timer
                _playheadUpdateTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                _playheadUpdateTimer?.Dispose();
                _playheadUpdateTimer = null;
                System.Console.WriteLine($"[EXEC ENGINE] Playhead update timer stopped");

                _executionTimer?.Stop();
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task WaitUntilAsync(TimeSpan targetTime, CancellationToken ct)
        {
            while (_executionTimer.Elapsed < targetTime && !ct.IsCancellationRequested)
            {
                // Wait with pause support
                _pauseEvent.Wait(1, ct);

                // High precision wait for last millisecond
                if (targetTime - _executionTimer.Elapsed < TimeSpan.FromMilliseconds(10))
                {
                    // Spin wait for high precision
                    SpinWait.SpinUntil(() => _executionTimer.Elapsed >= targetTime || ct.IsCancellationRequested);
                }
                else
                {
                    await Task.Delay(1, ct);
                }
            }
        }

        private async Task ExecuteEventAsync(SignalEvent evt, CancellationToken ct)
        {
            System.Console.WriteLine($"[EXEC ENGINE] ExecuteEventAsync: {evt.Name}, Type: {evt.EventType}, Device: {evt.DeviceType}, Model: {evt.DeviceModel}, Channel: {evt.Channel}");
            
            // CRITICAL: Select correct device controller based on event's DeviceModel
            if (string.IsNullOrEmpty(evt.DeviceModel))
            {
                throw new InvalidOperationException($"Event '{evt.Name}' has no DeviceModel specified. Cannot route to correct device.");
            }
            
            if (!_deviceControllers.TryGetValue(evt.DeviceModel, out var controller))
            {
                throw new InvalidOperationException($"No controller found for device '{evt.DeviceModel}'. Available: {string.Join(", ", _deviceControllers.Keys)}");
            }
            
            System.Console.WriteLine($"[EXEC ENGINE] Selected controller for device: {evt.DeviceModel}");
            
            switch (evt.EventType)
            {
                case SignalEventType.DC:
                    if (!evt.Parameters.ContainsKey("voltage"))
                        throw new InvalidOperationException("DC event requires 'voltage' parameter.");
                    
                    System.Console.WriteLine($"[EXEC ENGINE] Executing DC on {evt.DeviceModel}: Channel {evt.Channel}, Voltage {evt.Parameters["voltage"]}V");
                    controller.SetChannelValue(evt.Channel, evt.Parameters["voltage"]);
                    System.Console.WriteLine($"[EXEC ENGINE] DC executed successfully on {evt.DeviceModel}");
                    break;

                case SignalEventType.Ramp:
                    if (!evt.Parameters.ContainsKey("startVoltage") || !evt.Parameters.ContainsKey("endVoltage"))
                        throw new InvalidOperationException("Ramp event requires 'startVoltage' and 'endVoltage' parameters.");

                    double startV = evt.Parameters["startVoltage"];
                    double endV = evt.Parameters["endVoltage"];
                    
                    System.Console.WriteLine($"[EXEC ENGINE] Executing Ramp on {evt.DeviceModel}: Channel {evt.Channel}, {startV}V → {endV}V, Duration {evt.Duration.TotalMilliseconds}ms");
                    
                    // Set initial voltage
                    controller.SetChannelValue(evt.Channel, startV);
                    
                    // Execute ramp to end voltage
                    await controller.RampChannelValue(
                        evt.Channel,
                        endV,
                        (int)evt.Duration.TotalMilliseconds);
                    System.Console.WriteLine($"[EXEC ENGINE] Ramp executed successfully on {evt.DeviceModel}");
                    break;

                case SignalEventType.Waveform:
                    if (!evt.Parameters.ContainsKey("frequency") || 
                        !evt.Parameters.ContainsKey("amplitude") ||
                        !evt.Parameters.ContainsKey("offset"))
                        throw new InvalidOperationException("Waveform event requires 'frequency', 'amplitude', and 'offset' parameters.");

                    System.Console.WriteLine($"[EXEC ENGINE] Executing Waveform on {evt.DeviceModel}: Ch {evt.Channel}, Freq {evt.Parameters["frequency"]}Hz");
                    controller.StartSignalGeneration(
                        evt.Channel,
                        evt.Parameters["frequency"],
                        evt.Parameters["amplitude"],
                        evt.Parameters["offset"]);

                    // Wait for duration
                    await Task.Delay(evt.Duration, ct);

                    // Stop signal generation
                    controller.StopSignalGeneration();
                    System.Console.WriteLine($"[EXEC ENGINE] Waveform stopped on {evt.DeviceModel}");
                    break;

                case SignalEventType.DigitalPulse:
                    if (!evt.Parameters.ContainsKey("state"))
                        throw new InvalidOperationException("Digital pulse event requires 'state' parameter.");

                    int port = evt.Channel / 8;
                    int bit = evt.Channel % 8;
                    bool state = evt.Parameters["state"] > 0.5;

                    System.Console.WriteLine($"[EXEC ENGINE] Executing Digital Pulse on {evt.DeviceModel}: Port {port}, Bit {bit}, Duration {evt.Duration.TotalMilliseconds}ms");
                    // Set to ON
                    controller.WriteDigitalBit(port, bit, state);

                    // Wait for duration
                    await Task.Delay(evt.Duration, ct);

                    // Set to OFF
                    controller.WriteDigitalBit(port, bit, !state);
                    System.Console.WriteLine($"[EXEC ENGINE] Digital Pulse completed on {evt.DeviceModel}");
                    break;

                case SignalEventType.DigitalState:
                    if (!evt.Parameters.ContainsKey("state"))
                        throw new InvalidOperationException("Digital state event requires 'state' parameter.");

                    port = evt.Channel / 8;
                    bit = evt.Channel % 8;
                    state = evt.Parameters["state"] > 0.5;

                    System.Console.WriteLine($"[EXEC ENGINE] Executing Digital State on {evt.DeviceModel}: Port {port}, Bit {bit}, State {state}");
                    controller.WriteDigitalBit(port, bit, state);
                    System.Console.WriteLine($"[EXEC ENGINE] Digital state executed successfully on {evt.DeviceModel}");
                    break;

                case SignalEventType.Wait:
                    // Just wait, no output change
                    await Task.Delay(evt.Duration, ct);
                    break;

                default:
                    throw new NotSupportedException($"Event type {evt.EventType} is not supported.");
            }
        }

        public void Pause()
        {
            if (State != ExecutionState.Running)
                return;

            _pauseEvent.Reset();
            State = ExecutionState.Paused;
        }

        public void Resume()
        {
            if (State != ExecutionState.Paused)
                return;

            _pauseEvent.Set();
            State = ExecutionState.Running;
        }

        public void Stop()
        {
            if (State == ExecutionState.Idle || State == ExecutionState.Completed)
                return;

            _cts?.Cancel();
            _pauseEvent.Set(); // Unblock if paused
            State = ExecutionState.Stopping;
        }

        protected virtual void OnStateChanged(ExecutionStateChangedEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        protected virtual void OnEventExecuted(EventExecutedEventArgs e)
        {
            EventExecuted?.Invoke(this, e);
        }

        protected virtual void OnExecutionError(ExecutionErrorEventArgs e)
        {
            ExecutionError?.Invoke(this, e);
        }

        /// <summary>
        /// Callback for continuous playhead updates during execution
        /// </summary>
        private void UpdatePlayheadCallback(object state)
        {
            if (_executionTimer != null && _executionTimer.IsRunning && State == ExecutionState.Running)
            {
                CurrentTime = _executionTimer.Elapsed;
                // Note: CurrentTime setter fires PropertyChanged, which updates UI playhead
            }
        }

        /// <summary>
        /// Groups events by start time for parallel dispatch.
        /// Events within 1ms of each other are considered simultaneous.
        /// Returns a sorted dictionary: StartTime → List of concurrent events.
        /// </summary>
        private SortedDictionary<TimeSpan, List<SignalEvent>> GroupEventsByStartTime(List<SignalEvent> sortedEvents)
        {
            var groups = new SortedDictionary<TimeSpan, List<SignalEvent>>();
            const double toleranceMs = 1.0; // Events within 1ms are concurrent

            foreach (var evt in sortedEvents)
            {
                // Find existing group within tolerance
                TimeSpan matchedKey = TimeSpan.MinValue;
                foreach (var key in groups.Keys)
                {
                    if (Math.Abs((evt.StartTime - key).TotalMilliseconds) <= toleranceMs)
                    {
                        matchedKey = key;
                        break;
                    }
                }

                if (matchedKey != TimeSpan.MinValue)
                {
                    groups[matchedKey].Add(evt);
                }
                else
                {
                    groups[evt.StartTime] = new List<SignalEvent> { evt };
                }
            }

            return groups;
        }

        /// <summary>
        /// Wraps ExecuteEventAsync with error handling for parallel execution.
        /// Exceptions are caught and reported via OnExecutionError instead of propagating,
        /// so one failing event doesn't kill all parallel siblings.
        /// </summary>
        private async Task ExecuteEventWithErrorHandling(SignalEvent evt, CancellationToken ct)
        {
            try
            {
                await ExecuteEventAsync(evt, ct);
                System.Console.WriteLine($"[EXEC ENGINE] Parallel event completed: {evt.Name}");
                OnEventExecuted(new EventExecutedEventArgs { Event = evt, ActualTime = CurrentTime });
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[EXEC ENGINE] Parallel event failed: {evt.Name} - {ex.Message}");
                OnExecutionError(new ExecutionErrorEventArgs { Event = evt, Error = ex });
            }
        }
    }
}

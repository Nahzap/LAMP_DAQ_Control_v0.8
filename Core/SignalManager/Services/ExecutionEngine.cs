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
        private CancellationTokenSource _cts;
        private ManualResetEventSlim _pauseEvent;
        private SignalSequence _currentSequence;

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
            private set { lock (this) { _currentTime = value; } }
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

            try
            {
                // Sort events by start time
                var sortedEvents = sequence.GetEventsSorted();
                System.Console.WriteLine($"[EXEC ENGINE] Found {sortedEvents.Count()} events to execute");

                foreach (var evt in sortedEvents)
                {
                    System.Console.WriteLine($"[EXEC ENGINE] Processing event: {evt.Name} (Type: {evt.EventType}, Device: {evt.DeviceType}, Channel: {evt.Channel}, Start: {evt.StartTime.TotalSeconds:F2}s)");
                    
                    // Check for cancellation
                    if (_cts.Token.IsCancellationRequested)
                    {
                        System.Console.WriteLine($"[EXEC ENGINE] Cancellation requested, stopping");
                        State = ExecutionState.Stopping;
                        break;
                    }

                    // Wait for pause
                    _pauseEvent.Wait(_cts.Token);

                    // Wait until event start time
                    System.Console.WriteLine($"[EXEC ENGINE] Waiting until {evt.StartTime.TotalSeconds:F2}s...");
                    await WaitUntilAsync(evt.StartTime, _cts.Token);
                    System.Console.WriteLine($"[EXEC ENGINE] Time reached, executing event...");

                    // Execute event
                    try
                    {
                        await ExecuteEventAsync(evt, _cts.Token);
                        System.Console.WriteLine($"[EXEC ENGINE] Event executed successfully: {evt.Name}");

                        // Update current time
                        CurrentTime = _executionTimer.Elapsed;

                        // Fire event executed
                        OnEventExecuted(new EventExecutedEventArgs
                        {
                            Event = evt,
                            ActualTime = CurrentTime
                        });
                    }
                    catch (Exception ex)
                    {
                        OnExecutionError(new ExecutionErrorEventArgs
                        {
                            Event = evt,
                            Error = ex
                        });

                        // Continue execution unless critical error
                        if (ex is OperationCanceledException)
                            throw;
                    }
                }

                System.Console.WriteLine($"[EXEC ENGINE] All events executed successfully");
                State = ExecutionState.Completed;
                
                // Reset to Idle after completion to allow re-execution
                await Task.Delay(100); // Brief delay
                State = ExecutionState.Idle;
                System.Console.WriteLine($"[EXEC ENGINE] State reset to Idle - ready for next execution");
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
                    if (!evt.Parameters.ContainsKey("endVoltage"))
                        throw new InvalidOperationException("Ramp event requires 'endVoltage' parameter.");

                    System.Console.WriteLine($"[EXEC ENGINE] Executing Ramp on {evt.DeviceModel}: Channel {evt.Channel}, End {evt.Parameters["endVoltage"]}V, Duration {evt.Duration.TotalMilliseconds}ms");
                    await controller.RampChannelValue(
                        evt.Channel,
                        evt.Parameters["endVoltage"],
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
    }
}

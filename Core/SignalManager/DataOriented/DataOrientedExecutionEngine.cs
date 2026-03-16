using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Interfaces;
using System.Runtime.CompilerServices;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented
{
    /// <summary>
    /// Data-Oriented execution engine that operates directly on SignalTable.
    /// Optimized for cache-friendly iteration and high-performance execution.
    /// </summary>
    public class DataOrientedExecutionEngine
    {
        private readonly Dictionary<string, DAQController> _deviceControllers;
        private SignalTable _currentTable;
        private Stopwatch _executionTimer;
        private CancellationTokenSource _cts;
        private Timer _playheadUpdateTimer;
        private bool _isLoopEnabled;
        private long _totalSequenceDurationNs;
        
        // CRITICAL: Calibrated time conversion from Stopwatch ticks to nanoseconds
        private static readonly double _ticksToNanoseconds = (1_000_000_000.0 / Stopwatch.Frequency);
        
        public event EventHandler<ExecutionStateChangedEventArgs> StateChanged;
        public event EventHandler<EventExecutedEventArgs> EventExecuted;
        public event EventHandler<ExecutionErrorEventArgs> ExecutionError;
        
        public ExecutionState State { get; private set; }
        public TimeSpan CurrentTime { get; private set; }
        public TimeSpan TotalDuration => TimeSpan.FromTicks(_totalSequenceDurationNs / 100);
        public bool IsLoopEnabled
        {
            get => _isLoopEnabled;
            set => _isLoopEnabled = value;
        }
        
        public DataOrientedExecutionEngine(Dictionary<string, DAQController> deviceControllers)
        {
            _deviceControllers = deviceControllers ?? throw new ArgumentNullException(nameof(deviceControllers));
            State = ExecutionState.Idle;
            CurrentTime = TimeSpan.Zero;
            
            // CRITICAL: Log calibration info
            System.Console.WriteLine("[DO EXEC ENGINE] Initialized Data-Oriented Execution Engine");
            System.Console.WriteLine($"[DO TIMING CALIBRATION] Stopwatch Frequency: {Stopwatch.Frequency} Hz");
            System.Console.WriteLine($"[DO TIMING CALIBRATION] Ticks to Nanoseconds: {_ticksToNanoseconds:F6} ns/tick");
            System.Console.WriteLine($"[DO TIMING CALIBRATION] High Resolution: {Stopwatch.IsHighResolution}");
        }
        
        /// <summary>
        /// Executes a SignalTable directly (cache-friendly iteration)
        /// </summary>
        /// <param name="table">Signal table to execute</param>
        /// <param name="configuredDurationNs">Configured sequence duration in nanoseconds (0 = auto-calculate from events)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ExecuteTableAsync(SignalTable table, long configuredDurationNs = 0, CancellationToken cancellationToken = default)
        {
            if (State != ExecutionState.Idle)
            {
                System.Console.WriteLine($"[DO EXEC ENGINE ERROR] Cannot start - State is {State}, not Idle");
                throw new InvalidOperationException($"Execution already in progress (State: {State})");
            }
            
            _currentTable = table;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            try
            {
                // CRITICAL: Use configured duration if provided, otherwise calculate from events
                long eventsDurationNs = SignalOperations.CalculateTotalDuration(table);
                _totalSequenceDurationNs = (configuredDurationNs > 0) ? configuredDurationNs : eventsDurationNs;
                
                System.Console.WriteLine($"[DO EXEC ENGINE] Starting execution of table with {table.Count} events");
                System.Console.WriteLine($"[DO EXEC ENGINE] Events end at: {eventsDurationNs / 1e9:F3}s");
                System.Console.WriteLine($"[DO EXEC ENGINE] Configured sequence duration: {_totalSequenceDurationNs / 1e9:F3}s");
                
                State = ExecutionState.Running;
                OnStateChanged(ExecutionState.Running);
                
                // Start playhead update timer
                _executionTimer = Stopwatch.StartNew();
                _playheadUpdateTimer = new Timer(UpdatePlayheadCallback, null, 0, 16); // 60 FPS
                
                // Sort table by start time for sequential execution
                SignalOperations.SortByStartTime(table);
                
                // Execute all events sequentially
                await ExecuteEventsAsync(table, _cts.Token);
                
                System.Console.WriteLine("[DO EXEC ENGINE] All events executed successfully");
                
                // CRITICAL: Wait until total sequence duration is reached with high precision
                long elapsedNs = (long)(_executionTimer.ElapsedTicks * _ticksToNanoseconds);
                long remainingNs = _totalSequenceDurationNs - elapsedNs;
                
                if (remainingNs > 0 && !_cts.Token.IsCancellationRequested)
                {
                    System.Console.WriteLine($"[DO EXEC ENGINE] Waiting {remainingNs / 1e9:F6}s to complete sequence duration (elapsed: {elapsedNs / 1e9:F6}s, target: {_totalSequenceDurationNs / 1e9:F6}s)");
                    await HighPrecisionWaitAsync(remainingNs, _cts.Token);
                }
                
                System.Console.WriteLine($"[DO EXEC ENGINE] Sequence duration completed: {_executionTimer.Elapsed.TotalSeconds:F3}s");
                State = ExecutionState.Completed;
                OnStateChanged(ExecutionState.Completed);
                
                // Check loop
                bool shouldLoop = IsLoopEnabled;
                System.Console.WriteLine($"[DO EXEC ENGINE] Loop enabled: {shouldLoop}");
                
                if (shouldLoop && !_cts.Token.IsCancellationRequested)
                {
                    System.Console.WriteLine("[DO EXEC ENGINE] Loop enabled - restarting execution");
                    await Task.Delay(100);
                    
                    // Reset state
                    State = ExecutionState.Idle;
                    CurrentTime = TimeSpan.Zero;
                    
                    // Re-execute with same configured duration
                    await ExecuteTableAsync(table, _totalSequenceDurationNs, cancellationToken);
                    return;
                }
                else
                {
                    await Task.Delay(100);
                    State = ExecutionState.Idle;
                    CurrentTime = TimeSpan.Zero;
                    System.Console.WriteLine("[DO EXEC ENGINE] Execution completed");
                }
            }
            catch (OperationCanceledException)
            {
                System.Console.WriteLine("[DO EXEC ENGINE] Execution cancelled");
                State = ExecutionState.Idle;
                CurrentTime = TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[DO EXEC ENGINE ERROR] {ex.Message}");
                OnExecutionError(ex);
                State = ExecutionState.Idle;
                CurrentTime = TimeSpan.Zero;
                throw;
            }
            finally
            {
                _playheadUpdateTimer?.Dispose();
                _executionTimer?.Stop();
            }
        }
        
        /// <summary>
        /// Executes events from table (cache-friendly linear scan)
        /// </summary>
        private async Task ExecuteEventsAsync(SignalTable table, CancellationToken cancellationToken)
        {
            // Iterate through contiguous arrays (cache-friendly)
            for (int i = 0; i < table.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                long startNs = table.StartTimesNs[i];
                long durationNs = table.DurationsNs[i];
                
                // PRECISION TIMING: Wait until event start time using high-precision wait
                long elapsedNs = (long)(_executionTimer.ElapsedTicks * _ticksToNanoseconds);
                long waitNs = startNs - elapsedNs;
                
                if (waitNs > 0)
                {
                    System.Console.WriteLine($"[DO TIMING] Event {i}: waiting {waitNs / 1e9:F6}s (elapsed: {elapsedNs / 1e9:F6}s, start: {startNs / 1e9:F6}s)");
                    await HighPrecisionWaitAsync(waitNs, cancellationToken);
                }
                
                // Execute event (ramps wait internally, so this is correct)
                await ExecuteEventAtIndexAsync(table, i, cancellationToken);
            }
        }
        
        /// <summary>
        /// Executes a single event by index (no object allocation)
        /// </summary>
        private async Task ExecuteEventAtIndexAsync(SignalTable table, int index, CancellationToken cancellationToken)
        {
            var eventType = table.EventTypes[index];
            var deviceModel = table.DeviceModels[index];
            var channel = table.Channels[index];
            var durationNs = table.DurationsNs[index];
            
            System.Console.WriteLine($"[DO EXEC ENGINE] Executing {eventType} on {deviceModel} CH{channel}");
            
            if (!_deviceControllers.TryGetValue(deviceModel, out var controller))
            {
                System.Console.WriteLine($"[DO EXEC ENGINE ERROR] Device controller not found: {deviceModel}");
                return;
            }
            
            switch (eventType)
            {
                case SignalEventType.Ramp:
                    double startV = table.Attributes.GetStartVoltage(index, 0);
                    double endV = table.Attributes.GetEndVoltage(index, 0);
                    int durationMs = (int)(durationNs / 1_000_000);
                    
                    System.Console.WriteLine($"[DO EXEC ENGINE] Ramp: {startV}V → {endV}V over {durationMs}ms");
                    controller.SetChannelValue(channel, startV);
                    await controller.RampChannelValue(channel, endV, durationMs);
                    break;
                
                case SignalEventType.DC:
                    double voltage = table.Attributes.GetVoltage(index, 0);
                    System.Console.WriteLine($"[DO EXEC ENGINE] DC: {voltage}V for {durationNs / 1e9:F3}s");
                    controller.SetChannelValue(channel, voltage);
                    await Task.Delay(TimeSpan.FromTicks(durationNs / 100), cancellationToken);
                    break;
                
                case SignalEventType.Waveform:
                    var (freq, amp, offset) = table.Attributes.GetWaveformParams(index);
                    System.Console.WriteLine($"[DO EXEC ENGINE] Waveform: {freq}Hz, {amp}V amp, {offset}V offset");
                    // Implement waveform generation
                    await Task.Delay(TimeSpan.FromTicks(durationNs / 100), cancellationToken);
                    break;
                
                case SignalEventType.DigitalPulse:
                    System.Console.WriteLine($"[DO EXEC ENGINE] Digital Pulse");
                    int port = channel / 8;
                    int bit = channel % 8;
                    controller.WriteDigitalBit(port, bit, true);
                    await Task.Delay(TimeSpan.FromTicks(durationNs / 100), cancellationToken);
                    controller.WriteDigitalBit(port, bit, false);
                    break;
            }
        }
        
        public void Stop()
        {
            System.Console.WriteLine("[DO EXEC ENGINE] Stop requested");
            _cts?.Cancel();
        }
        
        public void Pause()
        {
            System.Console.WriteLine("[DO EXEC ENGINE] Pause not yet implemented");
        }
        
        private void UpdatePlayheadCallback(object state)
        {
            if (_executionTimer != null && _executionTimer.IsRunning && State == ExecutionState.Running)
            {
                var newTime = _executionTimer.Elapsed;
                if (newTime != CurrentTime)
                {
                    CurrentTime = newTime;
                    
                    // Fire event to update UI playhead (same pattern as OO engine)
                    OnEventExecuted(new EventExecutedEventArgs
                    {
                        Event = null,  // No specific event, just time update
                        ActualTime = CurrentTime
                    });
                }
            }
        }
        
        private void OnStateChanged(ExecutionState newState)
        {
            StateChanged?.Invoke(this, new ExecutionStateChangedEventArgs
            {
                OldState = State,
                NewState = newState
            });
        }
        
        private void OnEventExecuted(EventExecutedEventArgs args)
        {
            EventExecuted?.Invoke(this, args);
        }
        
        private void OnExecutionError(Exception ex)
        {
            ExecutionError?.Invoke(this, new ExecutionErrorEventArgs { Error = ex });
        }
        
        /// <summary>
        /// High-precision async wait using hybrid SpinWait + Task.Delay approach
        /// Uses SpinWait for sub-10ms delays, Task.Delay for longer waits
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task HighPrecisionWaitAsync(long waitNs, CancellationToken cancellationToken)
        {
            const long SPIN_THRESHOLD_NS = 10_000_000; // 10ms - below this, use SpinWait
            const long COARSE_WAIT_THRESHOLD_NS = 2_000_000; // 2ms - margin for Task.Delay imprecision
            
            long targetTicks = _executionTimer.ElapsedTicks + (long)(waitNs / _ticksToNanoseconds);
            
            // For long waits: use Task.Delay for bulk, then SpinWait for precision
            if (waitNs > SPIN_THRESHOLD_NS)
            {
                long coarseWaitNs = waitNs - COARSE_WAIT_THRESHOLD_NS;
                if (coarseWaitNs > 0)
                {
                    await Task.Delay(TimeSpan.FromTicks(coarseWaitNs / 100), cancellationToken);
                }
            }
            
            // PRECISION PHASE: SpinWait until exact target time
            SpinWait spinner = new SpinWait();
            while (_executionTimer.ElapsedTicks < targetTicks && !cancellationToken.IsCancellationRequested)
            {
                spinner.SpinOnce();
            }
        }
    }
}

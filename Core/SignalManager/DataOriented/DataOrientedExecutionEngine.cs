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
        private volatile bool _isLoopEnabled;
        private long _totalSequenceDurationNs;
        
        // CRITICAL: Track pending waveform stop tasks to await/cancel them between loop iterations
        private readonly List<Task> _pendingWaveformStops = new List<Task>();
        private readonly object _waveformStopsLock = new object();
        
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
            
            // CRITICAL: Use configured duration if provided, otherwise calculate from events
            long eventsDurationNs = SignalOperations.CalculateTotalDuration(table);
            _totalSequenceDurationNs = (configuredDurationNs > 0) ? configuredDurationNs : eventsDurationNs;
            
            // FIX: Use while-loop instead of recursion to prevent stack overflow and ensure proper cleanup
            bool keepLooping = true;
            while (keepLooping)
            {
                keepLooping = false; // Default: exit after one iteration
                
                // Create fresh CancellationTokenSource for this iteration
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                try
                {
                    System.Console.WriteLine($"[DO EXEC ENGINE] Starting execution of table with {table.Count} events");
                    System.Console.WriteLine($"[DO EXEC ENGINE] Events end at: {eventsDurationNs / 1e9:F3}s");
                    System.Console.WriteLine($"[DO EXEC ENGINE] Configured sequence duration: {_totalSequenceDurationNs / 1e9:F3}s");
                    
                    State = ExecutionState.Running;
                    OnStateChanged(ExecutionState.Running);
                    
                    // Clear pending waveform stop tasks from previous iteration
                    lock (_waveformStopsLock) { _pendingWaveformStops.Clear(); }
                    
                    // Start playhead update timer
                    _executionTimer = Stopwatch.StartNew();
                    _playheadUpdateTimer = new Timer(UpdatePlayheadCallback, null, 0, 16); // 60 FPS
                    
                    // Sort table by start time for sequential execution
                    SignalOperations.SortByStartTime(table);
                    
                    // Execute all events sequentially
                    await ExecuteEventsAsync(table, _cts.Token);
                    
                    System.Console.WriteLine("[DO EXEC ENGINE] All events executed successfully");
                    
                    // CRITICAL: Wait for all pending waveform stop tasks before proceeding
                    await WaitForPendingWaveformStopsAsync();
                    
                    // CRITICAL: Wait until total sequence duration is reached with high precision
                    long elapsedNs = (long)(_executionTimer.ElapsedTicks * _ticksToNanoseconds);
                    long remainingNs = _totalSequenceDurationNs - elapsedNs;
                    
                    if (remainingNs > 0 && !_cts.Token.IsCancellationRequested)
                    {
                        System.Console.WriteLine($"[DO EXEC ENGINE] Waiting {remainingNs / 1e9:F6}s to complete sequence duration (elapsed: {elapsedNs / 1e9:F6}s, target: {_totalSequenceDurationNs / 1e9:F6}s)");
                        await HighPrecisionWaitAsync(remainingNs, _cts.Token);
                    }
                    
                    // CRITICAL: Stop playhead timer BEFORE setting final time to prevent overshoot
                    _playheadUpdateTimer?.Dispose();
                    _playheadUpdateTimer = null;
                    
                    // Set final time exactly to configured duration
                    CurrentTime = TimeSpan.FromTicks(_totalSequenceDurationNs / 100);
                    System.Console.WriteLine($"[DO EXEC ENGINE] Sequence duration completed: {_executionTimer.Elapsed.TotalSeconds:F3}s, CurrentTime set to {CurrentTime.TotalSeconds:F3}s");
                    
                    State = ExecutionState.Completed;
                    OnStateChanged(ExecutionState.Completed);
                    
                    // Check loop (volatile read - thread-safe)
                    bool shouldLoop = _isLoopEnabled;
                    System.Console.WriteLine($"[DO EXEC ENGINE] Loop enabled: {shouldLoop}");
                    
                    if (shouldLoop && !_cts.Token.IsCancellationRequested)
                    {
                        System.Console.WriteLine("[DO EXEC ENGINE] Loop enabled - preparing restart...");
                        
                        // CRITICAL: Clean up hardware between loop iterations
                        CleanupBetweenLoopIterations();
                        
                        // Brief delay for UI to update
                        await Task.Delay(50);
                        
                        // Reset state for next iteration
                        State = ExecutionState.Idle;
                        CurrentTime = TimeSpan.Zero;
                        
                        // Dispose current CTS before next iteration creates a new one
                        _cts?.Dispose();
                        _cts = null;
                        _executionTimer?.Stop();
                        _executionTimer = null;
                        
                        System.Console.WriteLine("[DO EXEC ENGINE] Loop cleanup complete - restarting execution");
                        keepLooping = true; // Continue the while-loop
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
                    keepLooping = false;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[DO EXEC ENGINE ERROR] {ex.Message}");
                    System.Console.WriteLine($"[DO EXEC ENGINE ERROR] Stack: {ex.StackTrace}");
                    
                    // CRITICAL: Stop all signal generators on error to prevent hardware lock
                    System.Console.WriteLine("[DO EXEC ENGINE] Stopping all signal generators due to error...");
                    StopAllSignalGenerators();
                    
                    OnExecutionError(ex);
                    keepLooping = false;
                }
                finally
                {
                    // Always clean up resources for this iteration
                    _playheadUpdateTimer?.Dispose();
                    _playheadUpdateTimer = null;
                    _executionTimer?.Stop();
                    
                    if (!keepLooping)
                    {
                        // Final cleanup only when NOT looping
                        _cts?.Dispose();
                        _cts = null;
                        State = ExecutionState.Idle;
                        CurrentTime = TimeSpan.Zero;
                    }
                }
            }
        }
        
        /// <summary>
        /// Executes events from table with TRUE PARALLEL execution.
        /// CRITICAL FIX: Long-running events (PulseTrain, DC holds, DigitalState, Waveform) are
        /// launched as fire-and-forget background tasks. The engine does NOT wait for them to complete
        /// before advancing to the next time group. This ensures a 5s PulseTrain at t=0 does not
        /// block analog waveforms scheduled at t=1s.
        /// </summary>
        private async Task ExecuteEventsAsync(SignalTable table, CancellationToken cancellationToken)
        {
            // Track all background (fire-and-forget) tasks so we can await them at the end
            var backgroundTasks = new List<Task>();
            
            int i = 0;
            while (i < table.Count && !cancellationToken.IsCancellationRequested)
            {
                long currentGroupStartNs = table.StartTimesNs[i];
                
                // PRECISION TIMING: Wait until this group's start time
                long elapsedNs = (long)(_executionTimer.ElapsedTicks * _ticksToNanoseconds);
                long waitNs = currentGroupStartNs - elapsedNs;
                
                if (waitNs > 0)
                {
                    System.Console.WriteLine($"[DO TIMING] Group at {currentGroupStartNs / 1e9:F6}s: waiting {waitNs / 1e9:F6}s (elapsed: {elapsedNs / 1e9:F6}s)");
                    await HighPrecisionWaitAsync(waitNs, cancellationToken);
                }
                
                // Collect all events at this start time
                var groupIndices = new List<int>();
                while (i < table.Count && table.StartTimesNs[i] == currentGroupStartNs)
                {
                    groupIndices.Add(i);
                    i++;
                }
                
                // PHASE SYNC: Count waveform events in this parallel group
                int waveformCount = 0;
                foreach (int idx in groupIndices)
                {
                    if (table.EventTypes[idx] == SignalEventType.Waveform)
                        waveformCount++;
                }
                
                // PHASE SYNC: Prepare barrier and preload LUT if we have parallel waveforms
                if (waveformCount > 1)
                {
                    System.Console.WriteLine($"[PHASE SYNC] Detected {waveformCount} parallel waveforms - preparing synchronization");
                    DAQ.Services.SignalGenerator.PreloadLutCache();
                    DAQ.Services.SignalGenerator.PreparePhaseBarrier(waveformCount);
                }
                
                System.Console.WriteLine($"[DO PARALLEL] Launching {groupIndices.Count} events at {currentGroupStartNs / 1e9:F6}s");
                
                // Launch ALL events in this group simultaneously
                // Long-running events are fire-and-forget (tracked in backgroundTasks)
                // Short events (Ramp) complete naturally within their duration
                var immediateAwaitTasks = new List<Task>();
                
                foreach (int eventIndex in groupIndices)
                {
                    var eventType = table.EventTypes[eventIndex];
                    int capturedIndex = eventIndex;
                    
                    // Classify: is this a long-running event that may outlive its time group?
                    bool isLongRunning = (eventType == SignalEventType.PulseTrain ||
                                          eventType == SignalEventType.DC ||
                                          eventType == SignalEventType.DigitalState ||
                                          eventType == SignalEventType.DigitalPulse);
                    
                    var task = Task.Run(() => ExecuteEventAtIndexAsync(table, capturedIndex, cancellationToken), cancellationToken);
                    
                    if (isLongRunning)
                    {
                        // Fire-and-forget: don't block the timeline
                        backgroundTasks.Add(task);
                        System.Console.WriteLine($"[DO PARALLEL] Fire-and-forget: {eventType} on {table.DeviceModels[eventIndex]} CH{table.Channels[eventIndex]}");
                    }
                    else
                    {
                        // Ramp/Waveform: await in group (Ramp completes in its duration, Waveform returns immediately after starting signal gen)
                        immediateAwaitTasks.Add(task);
                        System.Console.WriteLine($"[DO PARALLEL] Await: {eventType} on {table.DeviceModels[eventIndex]} CH{table.Channels[eventIndex]}");
                    }
                }
                
                // Await only the non-long-running events before advancing to next time group
                if (immediateAwaitTasks.Count > 0)
                {
                    await Task.WhenAll(immediateAwaitTasks);
                }
                
                if (waveformCount > 1)
                {
                    System.Console.WriteLine($"[PHASE SYNC] {waveformCount} waveform threads launched - barrier will sync them internally");
                }
                
                System.Console.WriteLine($"[DO PARALLEL] Time group {currentGroupStartNs / 1e9:F6}s dispatched (background: {backgroundTasks.Count} active)");
            }
            
            // Wait for all background tasks to complete (they will finish at their scheduled end times)
            if (backgroundTasks.Count > 0)
            {
                System.Console.WriteLine($"[DO PARALLEL] Waiting for {backgroundTasks.Count} background tasks to complete...");
                try
                {
                    await Task.WhenAll(backgroundTasks);
                }
                catch (OperationCanceledException)
                {
                    // Expected on cancellation
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[DO PARALLEL ERROR] Background task error: {ex.Message}");
                }
                System.Console.WriteLine("[DO PARALLEL] All background tasks completed");
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
                    
                    // Write voltage at START
                    controller.SetChannelValue(channel, voltage);
                    
                    // Wait for duration
                    await Task.Delay(TimeSpan.FromTicks(durationNs / 100), cancellationToken);
                    
                    // Write voltage at END to ensure channel stays at correct value
                    controller.SetChannelValue(channel, voltage);
                    System.Console.WriteLine($"[DO DC END] CH{channel} confirmed at {voltage}V");
                    break;
                
                case SignalEventType.Waveform:
                    var (freq, amp, offset) = table.Attributes.GetWaveformParams(index);
                    System.Console.WriteLine($"[DO EXEC ENGINE] Waveform: {freq}Hz, {amp}V amp, {offset}V offset");
                    
                    // Start signal generation (non-blocking - runs on background thread)
                    controller.StartSignalGeneration(channel, freq, amp, offset);
                    
                    // Schedule stop after duration (non-blocking - allows parallelism)
                    long stopTimeNs = table.StartTimesNs[index] + durationNs;
                    int capturedChannel = channel; // Capture for closure
                    string capturedDeviceModel = deviceModel; // Capture for closure
                    var stopTask = Task.Run(async () =>
                    {
                        try
                        {
                            // Wait until scheduled stop time
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                var timer = _executionTimer;
                                if (timer == null || !timer.IsRunning) break;
                                
                                long elapsedNs = (long)(timer.ElapsedTicks * _ticksToNanoseconds);
                                long remainingNs = stopTimeNs - elapsedNs;
                                
                                if (remainingNs <= 0)
                                {
                                    // CRITICAL: Stop only THIS channel, not all channels
                                    var signalGen = controller.GetSignalGenerator();
                                    if (signalGen != null)
                                    {
                                        signalGen.StopChannel(capturedChannel);
                                        System.Console.WriteLine($"[DO WAVEFORM STOP] {capturedDeviceModel} CH{capturedChannel} stopped at {stopTimeNs / 1e9:F3}s");
                                    }
                                    break;
                                }
                                
                                await Task.Delay(1, cancellationToken); // Check every 1ms (minimize jitter)
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // On cancel, ensure channel is stopped
                            try
                            {
                                var signalGen = controller.GetSignalGenerator();
                                signalGen?.StopChannel(capturedChannel);
                            }
                            catch (Exception ex)
                            {
                                System.Console.WriteLine($"[DO WAVEFORM STOP ERROR] Failed to stop {capturedDeviceModel} CH{capturedChannel}: {ex.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"[DO WAVEFORM STOP ERROR] {capturedDeviceModel} CH{capturedChannel}: {ex.Message}");
                        }
                    }, cancellationToken);
                    
                    // CRITICAL: Track this task so we can await it before loop restart
                    RegisterWaveformStopTask(stopTask);
                    
                    // Return immediately to allow parallel execution
                    break;
                
                case SignalEventType.DigitalPulse:
                    System.Console.WriteLine($"[DO EXEC ENGINE] Digital Pulse");
                    int port = channel / 8;
                    int bit = channel % 8;
                    controller.WriteDigitalBit(port, bit, true);
                    await Task.Delay(TimeSpan.FromTicks(durationNs / 100), cancellationToken);
                    controller.WriteDigitalBit(port, bit, false);
                    break;
                
                case SignalEventType.DigitalState:
                    // Extract state from attributes (1.0 = HIGH, 0.0 = LOW)
                    double stateValue = table.Attributes.GetVoltage(index, 0);
                    bool state = stateValue > 0.5;
                    int portState = channel / 8;
                    int bitState = channel % 8;
                    
                    System.Console.WriteLine($"[DO EXEC ENGINE] Digital State: {(state ? "HIGH" : "LOW")} on {deviceModel} Port {portState} Bit {bitState}");
                    System.Console.WriteLine($"[DO DIGITAL CRITICAL] CALLING controller.WriteDigitalBit(port={portState}, bit={bitState}, state={state})");
                    System.Console.WriteLine($"[DO DIGITAL CRITICAL] Controller found: {controller != null}, DeviceModel in table: {deviceModel}");
                    
                    try
                    {
                        controller.WriteDigitalBit(portState, bitState, state);
                        System.Console.WriteLine($"[DO DIGITAL CRITICAL] ✓✓✓ WriteDigitalBit RETURNED SUCCESSFULLY ✓✓✓");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[DO DIGITAL ERROR] ❌ WriteDigitalBit THREW EXCEPTION: {ex.GetType().Name}");
                        System.Console.WriteLine($"[DO DIGITAL ERROR] Message: {ex.Message}");
                        System.Console.WriteLine($"[DO DIGITAL ERROR] Stack: {ex.StackTrace}");
                        throw;
                    }
                    
                    // CRITICAL: Wait for duration to maintain state
                    await Task.Delay(TimeSpan.FromTicks(durationNs / 100), cancellationToken);
                    
                    System.Console.WriteLine($"[DO EXEC ENGINE] Digital State completed on {deviceModel} CH{channel}");
                    break;
                
                case SignalEventType.PulseTrain:
                    // Extract PulseTrain parameters
                    var (frequency, dutyCycle, vHigh) = table.Attributes.GetWaveformParams(index);
                    double vLow = 0.0;
                    
                    System.Console.WriteLine($"[DO EXEC ENGINE] PulseTrain: {frequency}Hz, {dutyCycle * 100:F1}% duty, {vHigh:F1}V high");
                    
                    int portPT = channel / 8;
                    int bitPT = channel % 8;
                    
                    // Calculate timing
                    double periodMs = 1000.0 / frequency;
                    int highTimeMs = (int)(periodMs * dutyCycle);
                    int lowTimeMs = (int)(periodMs * (1.0 - dutyCycle));
                    
                    if (highTimeMs < 1) highTimeMs = 1;
                    if (lowTimeMs < 1) lowTimeMs = 1;
                    
                    long endTimeNs = table.StartTimesNs[index] + durationNs;
                    
                    System.Console.WriteLine($"[DO PULSE TRAIN] Period: {periodMs:F2}ms, High: {highTimeMs}ms, Low: {lowTimeMs}ms");
                    
                    // Generate pulse train until duration expires
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        long elapsedNs = (long)(_executionTimer.ElapsedTicks * _ticksToNanoseconds);
                        if (elapsedNs >= endTimeNs) break;
                        
                        // HIGH phase
                        controller.WriteDigitalBit(portPT, bitPT, true);
                        await Task.Delay(highTimeMs, cancellationToken);
                        
                        // Check if we should continue
                        elapsedNs = (long)(_executionTimer.ElapsedTicks * _ticksToNanoseconds);
                        if (elapsedNs >= endTimeNs) break;
                        
                        // LOW phase
                        controller.WriteDigitalBit(portPT, bitPT, false);
                        await Task.Delay(lowTimeMs, cancellationToken);
                    }
                    
                    // Ensure LOW at end
                    controller.WriteDigitalBit(portPT, bitPT, false);
                    System.Console.WriteLine($"[DO PULSE TRAIN] Completed on {deviceModel} CH{channel}");
                    break;
            }
        }
        
        public void Stop()
        {
            System.Console.WriteLine("[DO EXEC ENGINE] Stop requested");
            _cts?.Cancel();
            
            // CRITICAL: Also stop all signal generators immediately
            StopAllSignalGenerators();
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
        /// Registers a waveform stop task for tracking
        /// </summary>
        private void RegisterWaveformStopTask(Task task)
        {
            lock (_waveformStopsLock)
            {
                // Remove completed tasks to prevent list from growing indefinitely
                _pendingWaveformStops.RemoveAll(t => t.IsCompleted);
                _pendingWaveformStops.Add(task);
            }
        }
        
        /// <summary>
        /// Waits for all pending waveform stop tasks with timeout
        /// </summary>
        private async Task WaitForPendingWaveformStopsAsync()
        {
            Task[] tasks;
            lock (_waveformStopsLock)
            {
                tasks = _pendingWaveformStops.ToArray();
            }
            
            if (tasks.Length > 0)
            {
                System.Console.WriteLine($"[DO LOOP CLEANUP] Waiting for {tasks.Length} pending waveform stop tasks...");
                var completedInTime = await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(2000));
                
                // Check if any tasks are still pending
                int stillPending = 0;
                foreach (var t in tasks)
                {
                    if (!t.IsCompleted) stillPending++;
                }
                
                if (stillPending > 0)
                {
                    System.Console.WriteLine($"[DO LOOP CLEANUP WARNING] {stillPending} waveform stop tasks did not complete in 2s");
                }
                else
                {
                    System.Console.WriteLine($"[DO LOOP CLEANUP] All waveform stop tasks completed");
                }
            }
        }
        
        /// <summary>
        /// Cleans up hardware state between loop iterations
        /// </summary>
        private void CleanupBetweenLoopIterations()
        {
            System.Console.WriteLine("[DO LOOP CLEANUP] Stopping all signal generators between loop iterations...");
            StopAllSignalGenerators();
            System.Console.WriteLine("[DO LOOP CLEANUP] Hardware cleanup complete");
        }
        
        /// <summary>
        /// Safely stops all signal generators on all controllers
        /// </summary>
        private void StopAllSignalGenerators()
        {
            foreach (var kvp in _deviceControllers)
            {
                try
                {
                    kvp.Value.StopSignalGeneration();
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[DO EXEC ENGINE ERROR] Failed to stop signal gen on {kvp.Key}: {ex.Message}");
                }
            }
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

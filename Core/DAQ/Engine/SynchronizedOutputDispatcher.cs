using System;
using System.Diagnostics;
using System.Threading;
using LAMP_DAQ_Control_v0_8.Core.DAQ.HAL;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Engine
{
    /// <summary>
    /// Synchronized output dispatcher using ManualResetEventSlim for simultaneous digital+analog writes.
    /// 
    /// HIGH-02 FIX: Replaced Barrier with signal-based sync pattern.
    /// The Barrier had asymmetric timeouts (50ms vs 100ms) that caused phase desync on timeout,
    /// and Dispose() during Stop() was unsafe while threads were blocked in SignalAndWait().
    /// 
    /// HIGH-03 FIX: TriggerCycle now receives pre-consumed masks from the Metronome,
    /// eliminating the race condition where bits set between dispatch and clear were lost.
    /// 
    /// Architecture:
    ///   - Two dedicated threads: one for digital, one for analog
    ///   - Metronome sets _triggerEvent to release both threads simultaneously
    ///   - Each thread reads its pre-consumed mask, writes to hardware, signals done
    ///   - Metronome waits for both done signals before returning
    ///   - No computation in the write path — pure I/O for minimum jitter
    /// </summary>
    public class SynchronizedOutputDispatcher : IDisposable
    {
        private readonly IDigitalHal _digitalHal;
        private readonly IAnalogHal _analogHal;
        private readonly StateGrid _stateGrid;
        private readonly ILogger _logger;

        private Thread _digitalThread;
        private Thread _analogThread;
        private volatile bool _running;

        // HIGH-02 FIX: Signal-based sync replaces Barrier
        private readonly ManualResetEventSlim _triggerEvent;  // Set by Metronome to release write threads
        private readonly ManualResetEventSlim _digitalDone;   // Set by digital thread when write completes
        private readonly ManualResetEventSlim _analogDone;    // Set by analog thread when write completes

        // HIGH-03 FIX: Pre-consumed masks passed from Metronome
        private volatile uint _pendingDigitalMask;
        private volatile uint _pendingAnalogMask;

        // Track which threads are active
        private bool _hasDigitalThread;
        private bool _hasAnalogThread;

        // Statistics
        private long _digitalWriteCount;
        private long _analogWriteCount;
        private long _cycleCount;
        private long _maxJitterTicks;

        public long DigitalWriteCount => Interlocked.Read(ref _digitalWriteCount);
        public long AnalogWriteCount => Interlocked.Read(ref _analogWriteCount);
        public long CycleCount => Interlocked.Read(ref _cycleCount);

        /// <summary>
        /// Maximum observed jitter between digital and analog write completion (in ticks).
        /// </summary>
        public long MaxJitterTicks => Interlocked.Read(ref _maxJitterTicks);

        public bool IsRunning => _running;

        public SynchronizedOutputDispatcher(
            IDigitalHal digitalHal,
            IAnalogHal analogHal,
            StateGrid stateGrid,
            ILogger logger)
        {
            _digitalHal = digitalHal;
            _analogHal = analogHal;
            _stateGrid = stateGrid ?? throw new ArgumentNullException(nameof(stateGrid));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _triggerEvent = new ManualResetEventSlim(false);
            _digitalDone = new ManualResetEventSlim(true);  // Initially signaled (no pending work)
            _analogDone = new ManualResetEventSlim(true);   // Initially signaled (no pending work)
        }

        /// <summary>
        /// Starts the dispatcher threads.
        /// Both digital and analog HALs are optional — only active ones get a thread.
        /// </summary>
        public void Start()
        {
            if (_running)
                return;

            _running = true;

            _hasDigitalThread = _digitalHal != null && _digitalHal.IsReady;
            _hasAnalogThread = _analogHal != null && _analogHal.IsReady;

            if (!_hasDigitalThread && !_hasAnalogThread)
            {
                _logger.Warn("[Dispatcher] No HAL devices ready, dispatcher not started");
                _running = false;
                return;
            }

            if (_hasDigitalThread)
            {
                _digitalThread = new Thread(DigitalWriteLoop)
                {
                    Name = "DigitalOutputDispatcher",
                    Priority = ThreadPriority.Highest,
                    IsBackground = true
                };
                _digitalThread.Start();
            }

            if (_hasAnalogThread)
            {
                _analogThread = new Thread(AnalogWriteLoop)
                {
                    Name = "AnalogOutputDispatcher",
                    Priority = ThreadPriority.Highest,
                    IsBackground = true
                };
                _analogThread.Start();
            }

            _logger.Info($"[Dispatcher] Started (Digital={_hasDigitalThread}, Analog={_hasAnalogThread})");
        }

        /// <summary>
        /// Called by the OutputMetronome to trigger a synchronized write cycle.
        /// HIGH-03 FIX: Receives pre-consumed masks (atomically read-and-cleared by Metronome).
        /// HIGH-02 FIX: Uses ManualResetEventSlim instead of Barrier for robust sync.
        /// </summary>
        public void TriggerCycle(uint digitalMask, uint analogMask)
        {
            if (!_running)
                return;

            try
            {
                // Store consumed masks for the write threads
                _pendingDigitalMask = digitalMask;
                _pendingAnalogMask = analogMask;

                // Reset done signals for active threads
                if (_hasDigitalThread) _digitalDone.Reset();
                if (_hasAnalogThread) _analogDone.Reset();

                // Release write threads simultaneously
                _triggerEvent.Set();

                // Wait for both threads to complete their writes (50ms timeout)
                bool allDone = true;
                if (_hasDigitalThread)
                    allDone &= _digitalDone.Wait(50);
                if (_hasAnalogThread)
                    allDone &= _analogDone.Wait(50);

                if (!allDone)
                {
                    _logger.Warn("[Dispatcher] Write threads did not complete within 50ms timeout");
                }

                Interlocked.Increment(ref _cycleCount);

                // Reset trigger for next cycle
                _triggerEvent.Reset();
            }
            catch (ObjectDisposedException)
            {
                // Shutting down
            }
            catch (Exception ex)
            {
                _logger.Error("[Dispatcher] TriggerCycle error", ex);
            }
        }

        /// <summary>
        /// Backward-compatible overload (no masks). Reads masks from StateGrid directly.
        /// Used by any legacy callers that haven't been updated.
        /// </summary>
        public void TriggerCycle()
        {
            uint digitalMask = _stateGrid.ConsumeDigitalOutputMask();
            uint analogMask = _stateGrid.ConsumeAnalogOutputMask();
            if (digitalMask != 0 || analogMask != 0)
            {
                TriggerCycle(digitalMask, analogMask);
            }
        }

        /// <summary>
        /// Digital output thread. Waits for trigger, then writes changed ports.
        /// HIGH-02 FIX: Uses ManualResetEventSlim instead of Barrier.
        /// </summary>
        private void DigitalWriteLoop()
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                while (_running)
                {
                    try
                    {
                        // Wait for trigger from Metronome (100ms timeout for shutdown check)
                        if (!_triggerEvent.Wait(100))
                            continue; // Timeout — re-check _running

                        // Read pre-consumed mask
                        uint requiredMask = _pendingDigitalMask;
                        if (requiredMask != 0)
                        {
                            uint state = _stateGrid.DigitalOutputState;

                            // Determine which ports changed (convert channel mask to port mask)
                            byte portMask = 0;
                            if ((requiredMask & 0x000000FF) != 0) portMask |= 0x01; // Port 0
                            if ((requiredMask & 0x0000FF00) != 0) portMask |= 0x02; // Port 1
                            if ((requiredMask & 0x00FF0000) != 0) portMask |= 0x04; // Port 2
                            if ((requiredMask & 0xFF000000) != 0) portMask |= 0x08; // Port 3

                            _digitalHal.WriteOutputsMasked(state, portMask);
                            Interlocked.Increment(ref _digitalWriteCount);
                        }
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _logger.Error("[Dispatcher] Digital write error", ex);
                        Thread.Sleep(1); // Prevent tight error loop
                    }
                    finally
                    {
                        // Always signal done, even on error
                        try { _digitalDone.Set(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[Dispatcher] Digital thread crashed", ex);
            }
            finally
            {
                try { Thread.CurrentThread.Priority = ThreadPriority.Normal; } catch { }
            }
        }

        /// <summary>
        /// Analog output thread. Waits for trigger, then writes active channels.
        /// HIGH-02 FIX: Uses ManualResetEventSlim instead of Barrier.
        /// </summary>
        private void AnalogWriteLoop()
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                while (_running)
                {
                    try
                    {
                        // Wait for trigger from Metronome (100ms timeout for shutdown check)
                        if (!_triggerEvent.Wait(100))
                            continue; // Timeout — re-check _running

                        // Read pre-consumed mask
                        uint requiredMask = _pendingAnalogMask;
                        if (requiredMask != 0)
                        {
                            // Swap buffers (double buffering)
                            double[] voltages = _stateGrid.SwapAnalogBuffer();

                            _analogHal.WriteOutputs(voltages, requiredMask);
                            Interlocked.Increment(ref _analogWriteCount);
                        }
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _logger.Error("[Dispatcher] Analog write error", ex);
                        Thread.Sleep(1); // Prevent tight error loop
                    }
                    finally
                    {
                        // Always signal done, even on error
                        try { _analogDone.Set(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[Dispatcher] Analog thread crashed", ex);
            }
            finally
            {
                try { Thread.CurrentThread.Priority = ThreadPriority.Normal; } catch { }
            }
        }

        /// <summary>
        /// HIGH-02 FIX: Clean shutdown sequence.
        /// 1. Set _running=false
        /// 2. Set _triggerEvent to wake blocked threads
        /// 3. Join threads with timeout
        /// 4. THEN dispose events
        /// </summary>
        public void Stop()
        {
            if (!_running)
                return;

            _running = false;

            // Wake any blocked threads so they can check _running and exit
            try { _triggerEvent.Set(); } catch { }

            if (_digitalThread != null && _digitalThread.IsAlive)
                _digitalThread.Join(500);
            if (_analogThread != null && _analogThread.IsAlive)
                _analogThread.Join(500);

            _digitalThread = null;
            _analogThread = null;

            _logger.Info($"[Dispatcher] Stopped. Digital writes: {DigitalWriteCount}, Analog writes: {AnalogWriteCount}, Cycles: {CycleCount}");
        }

        public void Dispose()
        {
            Stop();
            _triggerEvent?.Dispose();
            _digitalDone?.Dispose();
            _analogDone?.Dispose();
        }
    }
}

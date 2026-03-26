using System;
using System.Diagnostics;
using System.Threading;
using LAMP_DAQ_Control_v0_8.Core.DAQ.HAL;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Engine
{
    /// <summary>
    /// Synchronized output dispatcher using Barrier for simultaneous digital+analog writes.
    /// 
    /// Architecture:
    ///   - Two dedicated threads: one for digital, one for analog
    ///   - Both wait on a Barrier signal from the OutputMetronome
    ///   - When released simultaneously, both write to their respective hardware
    ///   - Digital thread writes only changed ports (masked)
    ///   - Analog thread writes only active channels (masked)
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

        // Barrier for synchronized release: 3 participants (digital, analog, metronome)
        private Barrier _syncBarrier;

        // Signal from Metronome that a new cycle is ready
        private readonly ManualResetEventSlim _cycleReady;

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

            _cycleReady = new ManualResetEventSlim(false);
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

            bool hasDigital = _digitalHal != null && _digitalHal.IsReady;
            bool hasAnalog = _analogHal != null && _analogHal.IsReady;

            if (!hasDigital && !hasAnalog)
            {
                _logger.Warn("[Dispatcher] No HAL devices ready, dispatcher not started");
                _running = false;
                return;
            }

            // Barrier participants = active threads + 1 (metronome caller)
            int participants = 1; // metronome always participates
            if (hasDigital) participants++;
            if (hasAnalog) participants++;

            _syncBarrier = new Barrier(participants);

            if (hasDigital)
            {
                _digitalThread = new Thread(DigitalWriteLoop)
                {
                    Name = "DigitalOutputDispatcher",
                    Priority = ThreadPriority.Highest,
                    IsBackground = true
                };
                _digitalThread.Start();
            }

            if (hasAnalog)
            {
                _analogThread = new Thread(AnalogWriteLoop)
                {
                    Name = "AnalogOutputDispatcher",
                    Priority = ThreadPriority.Highest,
                    IsBackground = true
                };
                _analogThread.Start();
            }

            _logger.Info($"[Dispatcher] Started (Digital={hasDigital}, Analog={hasAnalog}, Barrier participants={participants})");
        }

        /// <summary>
        /// Called by the OutputMetronome to trigger a synchronized write cycle.
        /// This method participates in the Barrier, releasing all threads simultaneously.
        /// </summary>
        public void TriggerCycle()
        {
            if (!_running || _syncBarrier == null)
                return;

            try
            {
                // Signal that a new cycle is ready
                _cycleReady.Set();

                // Participate in barrier — this releases all threads at the same instant
                _syncBarrier.SignalAndWait(50); // 50ms timeout to avoid deadlock

                Interlocked.Increment(ref _cycleCount);

                // Reset for next cycle
                _cycleReady.Reset();
            }
            catch (BarrierPostPhaseException)
            {
                // Post-phase action threw — non-critical
            }
            catch (OperationCanceledException)
            {
                // Shutting down
            }
            catch (Exception ex)
            {
                _logger.Error("[Dispatcher] TriggerCycle error", ex);
            }
        }

        /// <summary>
        /// Digital output thread. Waits at barrier, then writes changed ports.
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
                        // Wait at barrier for synchronized release
                        _syncBarrier.SignalAndWait(100);

                        // Read what needs to be written
                        uint requiredMask = _stateGrid.RequiredDigitalOutputMask;
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
                    catch (BarrierPostPhaseException) { }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _logger.Error("[Dispatcher] Digital write error", ex);
                        Thread.Sleep(1); // Prevent tight error loop
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
        /// Analog output thread. Waits at barrier, then writes active channels.
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
                        // Wait at barrier for synchronized release
                        _syncBarrier.SignalAndWait(100);

                        // Read what needs to be written
                        uint requiredMask = _stateGrid.RequiredAnalogOutputMask;
                        if (requiredMask != 0)
                        {
                            // Swap buffers (double buffering)
                            double[] voltages = _stateGrid.SwapAnalogBuffer();

                            _analogHal.WriteOutputs(voltages, requiredMask);
                            Interlocked.Increment(ref _analogWriteCount);
                        }
                    }
                    catch (BarrierPostPhaseException) { }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _logger.Error("[Dispatcher] Analog write error", ex);
                        Thread.Sleep(1); // Prevent tight error loop
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

        public void Stop()
        {
            if (!_running)
                return;

            _running = false;
            _cycleReady.Set(); // Wake any waiters

            // Dispose barrier first to unblock threads
            try { _syncBarrier?.Dispose(); } catch { }
            _syncBarrier = null;

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
            _cycleReady?.Dispose();
        }
    }
}

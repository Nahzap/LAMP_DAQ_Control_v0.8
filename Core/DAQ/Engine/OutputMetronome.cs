using System;
using System.Diagnostics;
using System.Threading;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Engine
{
    /// <summary>
    /// High-speed output metronome that dictates the exact moment
    /// calculated data is injected into the physical pins.
    /// 
    /// Architecture:
    ///   - Dedicated thread with critical priority
    ///   - Stopwatch + SpinWait for sub-millisecond precision
    ///   - Configurable cycle interval (default: 500µs = 2kHz)
    ///   - Triggers the SynchronizedOutputDispatcher each cycle
    ///   - Only triggers when there's pending output (dirty check)
    /// </summary>
    public class OutputMetronome : IDisposable
    {
        private readonly SynchronizedOutputDispatcher _dispatcher;
        private readonly StateGrid _stateGrid;
        private readonly ILogger _logger;

        private Thread _metronomeThread;
        private volatile bool _running;

        // Timing configuration
        private long _intervalTicks;
        private int _intervalMicroseconds;

        // Statistics
        private long _totalCycles;
        private long _skippedCycles;
        private long _maxLateTicks;

        /// <summary>
        /// Cycle interval in microseconds. Default: 500µs (2kHz).
        /// Can be changed while running.
        /// </summary>
        public int IntervalMicroseconds
        {
            get { return _intervalMicroseconds; }
            set
            {
                if (value < 100) value = 100;     // Min 100µs (10kHz)
                if (value > 100000) value = 100000; // Max 100ms (10Hz)
                _intervalMicroseconds = value;
                _intervalTicks = (long)((double)value / 1_000_000.0 * Stopwatch.Frequency);
                _logger.Info($"[Metronome] Interval set to {value}µs ({1_000_000.0 / value:F0}Hz)");
            }
        }

        public bool IsRunning => _running;
        public long TotalCycles => Interlocked.Read(ref _totalCycles);
        public long SkippedCycles => Interlocked.Read(ref _skippedCycles);

        public OutputMetronome(
            SynchronizedOutputDispatcher dispatcher,
            StateGrid stateGrid,
            ILogger logger)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _stateGrid = stateGrid ?? throw new ArgumentNullException(nameof(stateGrid));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Default: 500µs = 2kHz output rate
            IntervalMicroseconds = 500;
        }

        /// <summary>
        /// Starts the metronome thread.
        /// </summary>
        public void Start()
        {
            if (_running)
                return;

            _running = true;

            _metronomeThread = new Thread(MetronomeLoop)
            {
                Name = "OutputMetronome",
                Priority = ThreadPriority.Highest,
                IsBackground = true
            };
            _metronomeThread.Start();

            _logger.Info($"[Metronome] Started at {1_000_000.0 / _intervalMicroseconds:F0}Hz");
        }

        /// <summary>
        /// Main metronome loop. Uses Stopwatch + SpinWait for precision timing.
        /// Only triggers dispatcher when there are pending output changes.
        /// </summary>
        private void MetronomeLoop()
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                var sw = Stopwatch.StartNew();
                long nextTick = sw.ElapsedTicks + _intervalTicks;
                long statsReportTicks = sw.ElapsedTicks;

                while (_running)
                {
                    long now = sw.ElapsedTicks;

                    if (now >= nextTick)
                    {
                        // Calculate how late we are
                        long lateTicks = now - nextTick;
                        if (lateTicks > Interlocked.Read(ref _maxLateTicks))
                            Interlocked.Exchange(ref _maxLateTicks, lateTicks);

                        // Check if there's any pending output
                        bool hasPendingDigital = _stateGrid.RequiredDigitalOutputMask != 0;
                        bool hasPendingAnalog = _stateGrid.RequiredAnalogOutputMask != 0;

                        if (hasPendingDigital || hasPendingAnalog)
                        {
                            // Trigger synchronized write
                            _dispatcher.TriggerCycle();

                            // Clear output masks after dispatch
                            _stateGrid.ClearOutputMasks();
                            _stateGrid.RecordOutputWrite();

                            Interlocked.Increment(ref _totalCycles);
                        }
                        else
                        {
                            Interlocked.Increment(ref _skippedCycles);
                        }

                        // Schedule next tick (absolute timing, not relative)
                        nextTick += _intervalTicks;

                        // If we've fallen behind by more than 2 intervals, reset
                        if (sw.ElapsedTicks > nextTick + _intervalTicks * 2)
                        {
                            nextTick = sw.ElapsedTicks + _intervalTicks;
                        }
                    }

                    // Precision wait: hybrid SpinWait approach
                    long remaining = nextTick - sw.ElapsedTicks;
                    if (remaining > _intervalTicks / 2)
                    {
                        // Far from target: yield to OS
                        Thread.Sleep(0);
                    }
                    else if (remaining > 100)
                    {
                        // Close to target: spin with yield
                        Thread.SpinWait(10);
                    }
                    // else: tight spin (implicit — loop continues immediately)

                    // Periodic statistics (every ~5 seconds)
                    if (sw.ElapsedTicks - statsReportTicks > Stopwatch.Frequency * 5)
                    {
                        double elapsed = (double)(sw.ElapsedTicks - statsReportTicks) / Stopwatch.Frequency;
                        long cycles = Interlocked.Read(ref _totalCycles);
                        long skipped = Interlocked.Read(ref _skippedCycles);
                        double maxLateUs = (double)Interlocked.Read(ref _maxLateTicks) / Stopwatch.Frequency * 1_000_000;

                        _logger.Debug($"[Metronome] {cycles} writes, {skipped} idle, max late: {maxLateUs:F1}µs");

                        statsReportTicks = sw.ElapsedTicks;
                        Interlocked.Exchange(ref _maxLateTicks, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[Metronome] Loop crashed", ex);
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

            if (_metronomeThread != null && _metronomeThread.IsAlive)
                _metronomeThread.Join(500);
            _metronomeThread = null;

            _logger.Info($"[Metronome] Stopped. Total cycles: {TotalCycles}, Skipped: {SkippedCycles}");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

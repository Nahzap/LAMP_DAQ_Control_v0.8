using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using LAMP_DAQ_Control_v0_8.Core.DAQ.HAL;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Engine
{
    /// <summary>
    /// High-speed digital input monitor using a dedicated thread.
    /// Replaces Timer-based polling with continuous read loop + XOR change detection.
    /// 
    /// Architecture:
    ///   - Dedicated thread at highest priority
    ///   - Continuous while(true) read loop (no Thread.Sleep)
    ///   - XOR between current and previous state to detect changes
    ///   - On change: deposits InputChangeEvent into ConcurrentQueue
    ///   - Signal via ManualResetEventSlim for consumer notification
    /// </summary>
    public class HighSpeedInputPoller : IDisposable
    {
        private readonly IDigitalHal _hal;
        private readonly StateGrid _stateGrid;
        private readonly ILogger _logger;
        
        private Thread _pollThread;
        private volatile bool _running;
        private readonly byte[] _readBuffer = new byte[4];
        private uint _previousState;

        // Producer-consumer channel (ConcurrentQueue + signal, since .NET 4.7.2 lacks Channels)
        private readonly ConcurrentQueue<InputChangeEvent> _changeQueue;
        private readonly ManualResetEventSlim _changeSignal;

        // Statistics
        private long _totalReads;
        private long _totalChanges;
        private long _startTicks;

        /// <summary>
        /// Queue of input change events. Consumed by LogicPipeline/StateGrid.
        /// </summary>
        public ConcurrentQueue<InputChangeEvent> ChangeQueue => _changeQueue;

        /// <summary>
        /// Signaled when a new change is deposited. Consumer can Wait() on this.
        /// </summary>
        public ManualResetEventSlim ChangeSignal => _changeSignal;

        /// <summary>
        /// Whether the poller is currently running.
        /// </summary>
        public bool IsRunning => _running;

        /// <summary>
        /// Total number of hardware reads performed since start.
        /// </summary>
        public long TotalReads => Interlocked.Read(ref _totalReads);

        /// <summary>
        /// Total number of state changes detected since start.
        /// </summary>
        public long TotalChanges => Interlocked.Read(ref _totalChanges);

        public HighSpeedInputPoller(IDigitalHal hal, StateGrid stateGrid, ILogger logger)
        {
            _hal = hal ?? throw new ArgumentNullException(nameof(hal));
            _stateGrid = stateGrid ?? throw new ArgumentNullException(nameof(stateGrid));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _changeQueue = new ConcurrentQueue<InputChangeEvent>();
            _changeSignal = new ManualResetEventSlim(false);
        }

        /// <summary>
        /// Starts the high-speed polling thread.
        /// The thread runs at ThreadPriority.Highest for minimum latency.
        /// </summary>
        public void Start()
        {
            if (_running)
            {
                _logger.Warn("[InputPoller] Already running");
                return;
            }

            if (!_hal.IsReady)
            {
                _logger.Error("[InputPoller] Digital HAL is not ready");
                return;
            }

            _running = true;
            _startTicks = Stopwatch.GetTimestamp();

            // Do initial read to seed previous state
            if (_hal.ReadInputsRaw(_readBuffer))
            {
                _previousState = PackBytes(_readBuffer);
                _stateGrid.UpdateInputState(_previousState);
            }

            _pollThread = new Thread(PollLoop)
            {
                Name = "HighSpeedInputPoller",
                Priority = ThreadPriority.Highest,
                IsBackground = true
            };
            _pollThread.Start();

            _logger.Info("[InputPoller] Started (dedicated thread, continuous XOR detection)");
        }

        /// <summary>
        /// Stops the polling thread. Blocks until the thread exits (max 500ms).
        /// </summary>
        public void Stop()
        {
            if (!_running)
                return;

            _running = false;
            _changeSignal.Set(); // Wake up any waiters

            if (_pollThread != null && _pollThread.IsAlive)
            {
                _pollThread.Join(500);
            }
            _pollThread = null;

            // Log statistics
            long elapsed = Stopwatch.GetTimestamp() - _startTicks;
            double seconds = (double)elapsed / Stopwatch.Frequency;
            long reads = Interlocked.Read(ref _totalReads);
            long changes = Interlocked.Read(ref _totalChanges);

            if (seconds > 0)
            {
                _logger.Info($"[InputPoller] Stopped. {reads} reads in {seconds:F1}s ({reads / seconds:F0} reads/sec), {changes} changes detected");
            }
            else
            {
                _logger.Info("[InputPoller] Stopped");
            }
        }

        /// <summary>
        /// Main polling loop. Runs on dedicated thread.
        /// No Sleep, no Timer — pure continuous read with XOR change detection.
        /// Uses SpinWait to avoid 100% CPU while maintaining sub-millisecond response.
        /// </summary>
        private void PollLoop()
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                SpinWait spinner = new SpinWait();
                int consecutiveNoChange = 0;

                while (_running)
                {
                    if (!_hal.ReadInputsRaw(_readBuffer))
                    {
                        // Read failed — brief pause to avoid hammering a broken device
                        Thread.Sleep(1);
                        continue;
                    }

                    Interlocked.Increment(ref _totalReads);

                    uint currentState = PackBytes(_readBuffer);

                    // XOR to detect changes
                    uint delta = currentState ^ _previousState;

                    if (delta != 0)
                    {
                        // Change detected!
                        _previousState = currentState;
                        Interlocked.Increment(ref _totalChanges);

                        // Update StateGrid atomically
                        _stateGrid.UpdateInputState(currentState);

                        // Deposit event into queue
                        var evt = new InputChangeEvent
                        {
                            NewState = currentState,
                            Delta = delta,
                            Timestamp = Stopwatch.GetTimestamp()
                        };
                        _changeQueue.Enqueue(evt);

                        // Signal consumers
                        _changeSignal.Set();

                        consecutiveNoChange = 0;
                    }
                    else
                    {
                        consecutiveNoChange++;

                        // Adaptive backoff: spin harder when changes are frequent,
                        // yield more when idle to reduce CPU usage
                        if (consecutiveNoChange > 10000)
                        {
                            // Long idle: yield to OS (still sub-millisecond response)
                            Thread.Sleep(0);
                        }
                        else if (consecutiveNoChange > 1000)
                        {
                            // Medium idle: SpinWait with yield
                            spinner.SpinOnce();
                        }
                        else
                        {
                            // Active: tight spin for minimum latency
                            Thread.SpinWait(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[InputPoller] Poll loop crashed", ex);
            }
            finally
            {
                try { Thread.CurrentThread.Priority = ThreadPriority.Normal; } catch { }
            }
        }

        /// <summary>
        /// Packs 4 bytes into a 32-bit unsigned integer.
        /// byte[0] = bits 0-7 (Port 0), byte[1] = bits 8-15 (Port 1), etc.
        /// </summary>
        private static uint PackBytes(byte[] buffer)
        {
            return (uint)(buffer[0] |
                         (buffer[1] << 8) |
                         (buffer[2] << 16) |
                         (buffer[3] << 24));
        }

        public void Dispose()
        {
            Stop();
            _changeSignal?.Dispose();
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Engine
{
    /// <summary>
    /// Concurrent logic processor that reads input changes, executes algorithms,
    /// and writes computed results to the StateGrid output buffers.
    /// 
    /// Architecture:
    ///   - Driven by InputPoller change events (event-driven mode)
    ///   - OR by continuous cycle (polling mode)
    ///   - Reads active channel masks from StateGrid
    ///   - Computes voltages/states only for active channels (bitmask iteration)
    ///   - Writes results to StateGrid "Next State Buffer" — never touches hardware
    ///   - Supports pluggable channel processing via delegate
    /// </summary>
    public class LogicPipeline : IDisposable
    {
        private readonly StateGrid _stateGrid;
        private readonly HighSpeedInputPoller _inputPoller;
        private readonly ILogger _logger;

        private Thread _pipelineThread;
        private volatile bool _running;

        // Channel processing delegate: (channelIndex, inputState) => outputVoltage
        // Allows external code to define what happens when an input changes
        private volatile Action<uint, StateGrid> _processCallback;

        // Statistics
        private long _processedEvents;
        private long _processedCycles;

        public long ProcessedEvents => Interlocked.Read(ref _processedEvents);
        public long ProcessedCycles => Interlocked.Read(ref _processedCycles);
        public bool IsRunning => _running;

        public LogicPipeline(StateGrid stateGrid, HighSpeedInputPoller inputPoller, ILogger logger)
        {
            _stateGrid = stateGrid ?? throw new ArgumentNullException(nameof(stateGrid));
            _inputPoller = inputPoller;  // Can be null if no digital input monitoring needed
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sets the callback that processes input changes and computes output state.
        /// The callback receives (inputDelta, stateGrid) and should update 
        /// stateGrid digital/analog outputs as needed.
        /// </summary>
        public void SetProcessCallback(Action<uint, StateGrid> callback)
        {
            _processCallback = callback;
        }

        /// <summary>
        /// Starts the pipeline in event-driven mode (triggered by input changes).
        /// </summary>
        public void Start()
        {
            if (_running)
                return;

            _running = true;

            _pipelineThread = new Thread(PipelineLoop)
            {
                Name = "LogicPipeline",
                Priority = ThreadPriority.AboveNormal,
                IsBackground = true
            };
            _pipelineThread.Start();

            _logger.Info("[LogicPipeline] Started (event-driven mode)");
        }

        /// <summary>
        /// Main pipeline loop. Waits for input changes, then processes them.
        /// </summary>
        private void PipelineLoop()
        {
            try
            {
                while (_running)
                {
                    bool hasWork = false;

                    // Check for input changes from the poller
                    if (_inputPoller != null)
                    {
                        // Wait for change signal with timeout (to allow shutdown)
                        if (_inputPoller.ChangeSignal.Wait(10))
                        {
                            // Drain all queued changes
                            InputChangeEvent evt;
                            while (_inputPoller.ChangeQueue.TryDequeue(out evt))
                            {
                                ProcessInputChange(evt);
                                Interlocked.Increment(ref _processedEvents);
                                hasWork = true;
                            }

                            // Reset signal after draining
                            _inputPoller.ChangeSignal.Reset();
                        }
                    }
                    else
                    {
                        // No input poller — just yield
                        Thread.Sleep(1);
                    }

                    if (hasWork)
                    {
                        Interlocked.Increment(ref _processedCycles);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[LogicPipeline] Pipeline loop crashed", ex);
            }
        }

        /// <summary>
        /// Processes a single input change event.
        /// Calls the registered callback, or applies default pass-through logic.
        /// </summary>
        private void ProcessInputChange(InputChangeEvent evt)
        {
            var callback = _processCallback;
            if (callback != null)
            {
                try
                {
                    callback(evt.Delta, _stateGrid);
                }
                catch (Exception ex)
                {
                    _logger.Error("[LogicPipeline] Process callback error", ex);
                }
                return;
            }

            // Default behavior: mirror input changes to digital output
            // (pass-through — each input bit maps to corresponding output bit)
            if (evt.Delta != 0)
            {
                _stateGrid.SetDigitalOutputState(evt.NewState);
                _stateGrid.RequiredDigitalOutputMask = evt.Delta;
            }
        }

        /// <summary>
        /// Directly requests a digital output change without waiting for input events.
        /// Used by DAQController/ExecutionEngine for command-driven output.
        /// </summary>
        public void RequestDigitalWrite(int port, int bit, bool value)
        {
            _stateGrid.SetDigitalBit(port, bit, value);
        }

        /// <summary>
        /// Directly requests a full digital port write.
        /// </summary>
        public void RequestDigitalPortWrite(int port, byte value)
        {
            uint currentState = _stateGrid.DigitalOutputState;
            int shift = port * 8;
            uint mask = 0xFFu << shift;

            // Clear old port value, set new one
            uint newState = (currentState & ~mask) | ((uint)value << shift);
            _stateGrid.SetDigitalOutputState(newState);

            // Mark the port's bits as requiring update
            _stateGrid.RequiredDigitalOutputMask = mask;
        }

        /// <summary>
        /// Directly requests an analog voltage write.
        /// Used by DAQController/ExecutionEngine for command-driven output.
        /// </summary>
        public void RequestAnalogWrite(int channel, double voltage)
        {
            _stateGrid.SetAnalogVoltage(channel, voltage);
        }

        /// <summary>
        /// Requests multiple analog writes using a bitmask.
        /// Only channels with their bit set in activeMask are updated.
        /// </summary>
        public void RequestAnalogWriteBatch(double[] voltages, uint activeMask)
        {
            if (voltages == null)
                return;

            uint mask = activeMask;
            while (mask != 0)
            {
                // Find lowest set bit
                int ch = BitIndex(mask);
                if (ch < voltages.Length)
                {
                    _stateGrid.SetAnalogVoltage(ch, voltages[ch]);
                }
                mask &= (mask - 1); // Clear lowest set bit
            }
        }

        private static int BitIndex(uint v)
        {
            uint isolated = v & (uint)(-(int)v);
            int index = 0;
            while (isolated > 1) { isolated >>= 1; index++; }
            return index;
        }

        public void Stop()
        {
            if (!_running)
                return;

            _running = false;

            if (_pipelineThread != null && _pipelineThread.IsAlive)
                _pipelineThread.Join(500);
            _pipelineThread = null;

            _logger.Info($"[LogicPipeline] Stopped. Events: {ProcessedEvents}, Cycles: {ProcessedCycles}");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

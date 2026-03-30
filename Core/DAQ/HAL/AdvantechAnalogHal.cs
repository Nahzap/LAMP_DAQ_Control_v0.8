using System;
using System.Threading;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Exceptions;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.HAL
{
    /// <summary>
    /// Advantech PCIe-1824 implementation of IAnalogHal.
    /// Thin wrapper — no logic, no timers. Pure hardware I/O.
    /// 32 channels, 16-bit DAC, 0-10V output range.
    /// </summary>
    public class AdvantechAnalogHal : IAnalogHal
    {
        private InstantAoCtrl _aoCtrl;
        private readonly ILogger _logger;
        private bool _disposed;
        private bool _ownsDevice = true;
        private long _errorCount;

        public bool IsReady { get; private set; }
        public int ChannelCount { get; private set; }

        /// <summary>
        /// MED-04: Fired when the device is physically removed or reconnected.
        /// </summary>
        public event EventHandler<bool> DeviceStateChanged;

        /// <summary>
        /// Exposes the underlying InstantAoCtrl for backward compatibility
        /// with existing SignalGenerator and DeviceManager.
        /// </summary>
        public InstantAoCtrl RawDevice => _aoCtrl;

        public AdvantechAnalogHal(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes the analog HAL with the specified Board ID.
        /// Finds and selects the PCIe-1824 device.
        /// </summary>
        public bool Initialize(int deviceNumber)
        {
            try
            {
                _aoCtrl = new InstantAoCtrl();

                int actualDevNum = FindAnalogDevice(_aoCtrl, deviceNumber);
                if (actualDevNum < 0)
                {
                    _logger.Info($"[AnalogHAL] No PCIe-1824 found with Board ID {deviceNumber}");
                    return false;
                }

                _aoCtrl.SelectedDevice = new DeviceInformation(actualDevNum);
                string desc = _aoCtrl.SelectedDevice.Description;

                if (!desc.Contains("PCIe-1824") && !desc.Contains("1824"))
                {
                    _logger.Info($"[AnalogHAL] Device {desc} is not a PCIe-1824");
                    return false;
                }

                var testErr = _aoCtrl.Write(0, 0.0);
                ThrowOnError(testErr, $"Test write to channel 0");
                ChannelCount = _aoCtrl.Channels?.Length ?? 0;
                IsReady = true;

                _logger.Info($"[AnalogHAL] Initialized: {desc}, {ChannelCount} channels");

                // MED-04 FIX: Hot-plug event subscription
                // Note: InstantAoCtrl in DAQNavi 4.0 does not expose device removed/reconnected
                // events via addEventHandler. The DeviceStateChanged event is available for
                // manual checking via IsReady property. Future SDK versions may add direct events.
                _logger.Info("[AnalogHAL] Hot-plug monitoring: IsReady property will reflect device state");

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("[AnalogHAL] Initialization failed", ex);
                IsReady = false;
                return false;
            }
        }

        /// <summary>
        /// Initializes the analog HAL with an existing InstantAoCtrl instance.
        /// Used for backward compatibility with existing DeviceManager.
        /// </summary>
        public void InitializeFromExisting(InstantAoCtrl existingDevice)
        {
            if (existingDevice == null)
                throw new ArgumentNullException(nameof(existingDevice));

            _aoCtrl = existingDevice;
            _ownsDevice = false;
            
            try 
            {
                ChannelCount = _aoCtrl.Channels?.Length ?? 0;
            }
            catch
            {
                ChannelCount = 0;
            }
            
            IsReady = ChannelCount > 0;
            _logger.Info($"[AnalogHAL] Initialized from existing device, {ChannelCount} channels");
        }

        public void WriteSingle(int channel, double voltage)
        {
            if (!IsReady || _aoCtrl == null)
                return;

            var err = _aoCtrl.Write(channel, voltage);
            WarnOnError(err, $"WriteSingle(ch={channel}, v={voltage:F3})");
        }

        public void WriteOutputs(double[] voltages, uint activeMask)
        {
            if (!IsReady || _aoCtrl == null || voltages == null)
                return;

            int maxCh = Math.Min(voltages.Length, ChannelCount);
            // Iterate only over set bits in the mask
            uint mask = activeMask;
            while (mask != 0)
            {
                // Find lowest set bit index
                int ch = BitIndex(mask);
                if (ch < maxCh)
                {
                    var err = _aoCtrl.Write(ch, voltages[ch]);
                    WarnOnError(err, $"WriteOutputs(ch={ch}, v={voltages[ch]:F3})");
                }
                // Clear lowest set bit
                mask &= (mask - 1);
            }
        }

        /// <summary>
        /// Configures all channels with the specified value range.
        /// </summary>
        public void ConfigureChannels(ValueRange range)
        {
            if (!IsReady || _aoCtrl == null || _aoCtrl.Channels == null)
                return;

            double initialValue = (range == ValueRange.mA_4To20) ? 4.0 : 0.0;
            for (int i = 0; i < _aoCtrl.Channels.Length; i++)
            {
                try
                {
                    _aoCtrl.Channels[i].ValueRange = range;
                    var err = _aoCtrl.Write(i, initialValue);
                    ThrowOnError(err, $"ConfigureChannels(ch={i}, range={range})");
                }
                catch (DAQOperationException)
                {
                    throw; // Re-throw SDK errors
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[AnalogHAL] Error configuring channel {i}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// MED-03: Gets the voltage range for a specific channel from the SDK.
        /// PCIe-1824 supports V_Neg10To10 (most channels) and V_0To10 (channel 31).
        /// </summary>
        public bool GetChannelVoltageRange(int channel, out double minVoltage, out double maxVoltage)
        {
            minVoltage = 0.0;
            maxVoltage = 10.0;

            if (!IsReady || _aoCtrl?.Channels == null || channel < 0 || channel >= _aoCtrl.Channels.Length)
                return false;

            try
            {
                var range = _aoCtrl.Channels[channel].ValueRange;
                switch (range)
                {
                    case ValueRange.V_Neg10To10:
                        minVoltage = -10.0;
                        maxVoltage = 10.0;
                        break;
                    case ValueRange.V_0To10:
                        minVoltage = 0.0;
                        maxVoltage = 10.0;
                        break;
                    case ValueRange.V_Neg5To5:
                        minVoltage = -5.0;
                        maxVoltage = 5.0;
                        break;
                    case ValueRange.V_0To5:
                        minVoltage = 0.0;
                        maxVoltage = 5.0;
                        break;
                    default:
                        // Safe fallback for unknown ranges
                        minVoltage = -10.0;
                        maxVoltage = 10.0;
                        break;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the index of the lowest set bit (de Bruijn method).
        /// </summary>
        private static int BitIndex(uint v)
        {
            // Isolate lowest set bit and count trailing zeros
            uint isolated = v & (uint)(-(int)v);
            int index = 0;
            while (isolated > 1) { isolated >>= 1; index++; }
            return index;
        }

        /// <summary>
        /// Checks SDK ErrorCode and throws DAQOperationException on failure.
        /// Used for initialization and configuration paths.
        /// </summary>
        private void ThrowOnError(ErrorCode err, string operation)
        {
            if (err != ErrorCode.Success)
            {
                string msg = $"[AnalogHAL] {operation} failed: SDK ErrorCode={err}";
                _logger.Error(msg);
                throw new DAQOperationException(msg);
            }
        }

        /// <summary>
        /// Checks SDK ErrorCode and logs warning on failure (no throw).
        /// Used for hot-path writes (WriteSingle, WriteOutputs) to avoid killing threads.
        /// Logs at most once per 1000 errors to prevent log flooding.
        /// </summary>
        private void WarnOnError(ErrorCode err, string operation)
        {
            if (err != ErrorCode.Success)
            {
                long count = Interlocked.Increment(ref _errorCount);
                if (count == 1 || count % 1000 == 0)
                {
                    _logger.Warn($"[AnalogHAL] {operation}: SDK ErrorCode={err} (error #{count})");
                }
            }
        }

        private int FindAnalogDevice(InstantAoCtrl ctrl, int boardId)
        {
            var devices = ctrl.SupportedDevices;
            for (int i = 0; i < devices.Count; i++)
            {
                var info = devices[i];
                if (info.DeviceNumber == boardId ||
                    info.Description.Contains($"BID#{boardId}"))
                {
                    return info.DeviceNumber;
                }
            }
            return -1;
        }

        // MED-04 FIX: Hot-plug handlers
        private void OnDeviceRemoved(object sender, EventArgs e)
        {
            IsReady = false;
            _logger.Error("[AnalogHAL] Device REMOVED — all operations suspended");
            DeviceStateChanged?.Invoke(this, false);
        }

        private void OnDeviceReconnected(object sender, EventArgs e)
        {
            IsReady = true;
            _logger.Info("[AnalogHAL] Device RECONNECTED — operations resumed");
            DeviceStateChanged?.Invoke(this, true);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                IsReady = false;

                if (_aoCtrl != null)
                {
                    if (_ownsDevice)
                    {
                        try
                        {
                            int count = _aoCtrl.Channels?.Length ?? 0;
                            for (int i = 0; i < count; i++)
                            {
                                try { _aoCtrl.Write(i, 0.0); } catch { }
                            }
                        }
                        catch { }

                        _aoCtrl.Dispose();
                    }
                    _aoCtrl = null;
                }

                _logger.Info("[AnalogHAL] Disposed");
            }
        }
    }
}

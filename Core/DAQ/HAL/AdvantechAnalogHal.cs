using System;
using Automation.BDaq;
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

        public bool IsReady { get; private set; }
        public int ChannelCount { get; private set; }

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

                _aoCtrl.Write(0, 0.0);
                ChannelCount = _aoCtrl.Channels?.Length ?? 0;
                IsReady = true;

                _logger.Info($"[AnalogHAL] Initialized: {desc}, {ChannelCount} channels");
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
            ChannelCount = _aoCtrl.Channels?.Length ?? 0;
            IsReady = ChannelCount > 0;
            _logger.Info($"[AnalogHAL] Initialized from existing device, {ChannelCount} channels");
        }

        public void WriteSingle(int channel, double voltage)
        {
            if (!IsReady || _aoCtrl == null)
                return;

            _aoCtrl.Write(channel, voltage);
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
                    _aoCtrl.Write(ch, voltages[ch]);
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
                    _aoCtrl.Write(i, initialValue);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[AnalogHAL] Error configuring channel {i}: {ex.Message}");
                }
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

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                IsReady = false;

                if (_aoCtrl != null)
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
                    _aoCtrl = null;
                }

                _logger.Info("[AnalogHAL] Disposed");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Managers
{
    /// <summary>
    /// Manages channel operations for DAQ devices.
    /// HIGH-01 FIX: No longer accesses _deviceManager.Device (InstantAoCtrl).
    /// Uses abstract IDeviceManager methods that route correctly for both analog and digital.
    /// </summary>
    public class ChannelManager : IChannelManager
    {
        private readonly IDeviceManager _deviceManager;
        private readonly ILogger _logger;

        public ChannelManager(IDeviceManager deviceManager, ILogger logger = null)
        {
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _logger = logger ?? new ConsoleLogger();
            _logger.Info("Channel manager created successfully");
        }

        public IReadOnlyCollection<ChannelState> GetChannelStates(ISignalGenerator signalGenerator)
        {
            if (!_deviceManager.IsInitialized)
                throw new InvalidOperationException("Device is not initialized");

            var states = new List<ChannelState>();
            int channelCount = _deviceManager.ChannelCount;

            if (channelCount == 0)
                return Array.Empty<ChannelState>();

            for (int i = 0; i < channelCount; i++)
            {
                try
                {
                    bool isActive = signalGenerator.IsChannelActive(i);
                    
                    // HIGH-01 FIX: Use TryGetChannelInfo() instead of _deviceManager.Device.Channels[i]
                    string range;
                    if (!_deviceManager.TryGetChannelInfo(i, out range))
                        range = "Unknown";

                    states.Add(new ChannelState(i, 0.0, range, isActive));
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Error getting state for channel {i}: {ex.Message}");
                    states.Add(new ChannelState(i, 0.0, "Unknown", false));
                }
            }

            return states;
        }

        public void WriteVoltage(int channel, double value)
        {
            _deviceManager.WriteVoltage(channel, value);
        }

        public void ResetAllChannels()
        {
            if (!_deviceManager.IsInitialized)
                return;

            try
            {
                // HIGH-01 FIX: Use abstract ResetAllOutputs() instead of _deviceManager.Device.Write()
                _deviceManager.ResetAllOutputs();
                _logger.Info("All channels reset to default values");
            }
            catch (Exception ex)
            {
                _logger.Error("Error resetting channels", ex);
            }
        }

        public void ValidateChannelNumber(int channel)
        {
            if (!_deviceManager.IsInitialized)
                throw new InvalidOperationException("Device is not initialized");

            if (channel < 0 || channel >= _deviceManager.ChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channel), $"Channel number must be between 0 and {_deviceManager.ChannelCount - 1}");
        }
    }
}

using System;
using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Managers
{
    /// <summary>
    /// Manages channel operations for DAQ devices
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

            var device = _deviceManager.Device;
            if (device.Channels == null || device.Channels.Length == 0)
                return Array.Empty<ChannelState>();
            
            var states = new List<ChannelState>();
            
            for (int i = 0; i < device.Channels.Length; i++)
            {
                try
                {
                    // The Advantech PCIe-1824 doesn't support reading back analog output values
                    bool isActive = signalGenerator.IsChannelActive(i);
                    string range = device.Channels[i].ValueRange.ToString();
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
                var device = _deviceManager.Device;
                for (int i = 0; i < device.ChannelCount; i++)
                {
                    try
                    {
                        device.Write(i, 0.0);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Error resetting channel {i}: {ex.Message}");
                    }
                }
                
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

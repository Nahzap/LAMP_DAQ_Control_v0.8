using System;
using System.Collections.Generic;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Exceptions;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Managers
{
    /// <summary>
    /// Manages DAQ device initialization and operations
    /// </summary>
    public class DeviceManager : IDeviceManager, IDisposable
    {
        private readonly InstantAoCtrl _device;
        private readonly ILogger _logger;
        private bool _deviceInitialized;
        private bool _disposed;
        private string _deviceModel;
        private const int MAX_DEVICES_TO_CHECK = 4;

        public DeviceManager(ILogger logger = null)
        {
            _logger = logger ?? new ConsoleLogger();
            
            try
            {
                _device = new InstantAoCtrl();
                _logger.Info("Device manager created successfully");
            }
            catch (Exception ex)
            {
                const string errorMsg = "Error creating device manager";
                _logger.Error(errorMsg, ex);
                throw new DAQInitializationException(errorMsg, ex);
            }
        }

        public bool IsInitialized => _deviceInitialized && !_disposed;

        public int ChannelCount => _device?.Channels?.Length ?? 0;

        public string DeviceModel => _deviceModel ?? "Unknown";

        public InstantAoCtrl Device => _device;

        public void InitializeDevice(int deviceNumber)
        {
            EnsureNotDisposed();

            try
            {
                if (_deviceInitialized)
                {
                    _logger.Info("Device is already initialized");
                    return;
                }

                _device.SelectedDevice = new DeviceInformation(deviceNumber);
                _deviceModel = _device.SelectedDevice.Description;
                _logger.Info($"Initializing device: {_deviceModel} (Device {deviceNumber})");
                
                // Test communication with a write operation
                _device.Write(0, 0.0);
                _deviceInitialized = true;
                _logger.Info($"Device {deviceNumber} initialized successfully");
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to initialize DAQ device {deviceNumber}";
                _logger.Error(errorMsg, ex);
                throw new DAQInitializationException(errorMsg, ex);
            }
        }

        public void WriteVoltage(int channel, double value)
        {
            EnsureInitialized();
            ValidateChannelNumber(channel);

            try
            {
                _device.Write(channel, value);
                _logger.Debug($"Channel {channel} updated to {value}V");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error writing to channel {channel}", ex);
                throw new DAQOperationException($"Error writing to channel {channel}", ex);
            }
        }

        public IList<DeviceInfo> DetectDevices()
        {
            var devices = new List<DeviceInfo>();
            
            for (int i = 0; i < MAX_DEVICES_TO_CHECK; i++)
            {
                using (var daq = new InstantAoCtrl())
                {
                    try
                    {
                        daq.SelectedDevice = new DeviceInformation(i);
                        int channelCount = daq.Channels != null ? daq.Channels.Length : 0;
                        var deviceInfo = new DeviceInfo($"PCIe-1824 (ID: {i})", channelCount)
                        {
                            AdditionalInfo = "Device detected and accessible"
                        };
                        devices.Add(deviceInfo);
                        _logger.Debug($"Detected device {i}: {deviceInfo.Name}");
                    }
                    catch (Exception ex) when (i > 0)
                    {
                        // Skip logging for first device as it's common to not find device 0
                    }
                }
            }
            
            _logger.Info($"Detected {devices.Count} devices");
            return devices;
        }

        public DeviceInfo GetDeviceInfo()
        {
            EnsureInitialized();
            
            try
            {
                string deviceName = $"{_device.SelectedDevice.Description} (ID: {_device.SelectedDevice.DeviceNumber})";
                var info = new DeviceInfo(deviceName, _device.ChannelCount)
                {
                    AdditionalInfo = $"Status: {(_deviceInitialized ? "Initialized" : "Not Initialized")}"
                };
                
                _logger.Info($"Device Info - {info}");
                return info;
            }
            catch (Exception ex)
            {
                _logger.Error("Error getting device information", ex);
                throw new DAQOperationException("Failed to retrieve device information", ex);
            }
        }

        public void ConfigureChannels(ValueRange range)
        {
            EnsureInitialized();
            
            if (_device.Channels == null || _device.Channels.Length == 0)
                throw new DAQInitializationException("No channels found on the device");

            double initialValue = (range == ValueRange.mA_4To20) ? 4.0 : 0.0;
            
            _logger.Info($"Configuring {_device.Channels.Length} channels with range: {range}");

            for (int i = 0; i < _device.Channels.Length; i++)
            {
                try
                {
                    _device.Channels[i].ValueRange = range;
                    _device.Write(i, initialValue);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Error configuring channel {i}: {ex.Message}");
                }
            }
            
            _logger.Info("Channel configuration completed");
        }

        private void ValidateChannelNumber(int channel)
        {
            if (channel < 0 || (ChannelCount > 0 && channel >= ChannelCount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(channel), 
                    $"Channel must be between 0 and {ChannelCount - 1}");
            }
        }

        private void EnsureInitialized()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DeviceManager));
                
            if (!_deviceInitialized)
                throw new InvalidOperationException("Device is not initialized");
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DeviceManager));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Reset all outputs to 0V
                    if (_deviceInitialized && _device != null)
                    {
                        for (int i = 0; i < _device.ChannelCount; i++)
                        {
                            try { _device.Write(i, 0.0); }
                            catch { /* Ignore errors during cleanup */ }
                        }
                    }
                    
                    _device?.Dispose();
                    _logger.Info("Device resources released");
                }
                catch (Exception ex)
                {
                    _logger.Error("Error releasing device resources", ex);
                }
                
                _disposed = true;
                _deviceInitialized = false;
            }
        }

        ~DeviceManager()
        {
            Dispose(false);
        }
    }
}

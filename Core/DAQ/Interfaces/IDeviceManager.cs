using System;
using System.Collections.Generic;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces
{
    /// <summary>
    /// Interface for managing DAQ devices
    /// </summary>
    public interface IDeviceManager
    {
        /// <summary>
        /// Gets the underlying device instance
        /// </summary>
        InstantAoCtrl Device { get; }

        /// <summary>
        /// Gets a value indicating whether the device is initialized
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Gets the number of available channels on the device
        /// </summary>
        int ChannelCount { get; }

        /// <summary>
        /// Gets the detected device model
        /// </summary>
        string DeviceModel { get; }

        /// <summary>
        /// Initializes the device with the specified device number
        /// </summary>
        /// <param name="deviceNumber">Device number to initialize</param>
        void InitializeDevice(int deviceNumber);

        /// <summary>
        /// Writes a voltage value to the specified channel
        /// </summary>
        /// <param name="channel">Channel number (0-based)</param>
        /// <param name="value">Value to write (in Volts)</param>
        void WriteVoltage(int channel, double value);

        /// <summary>
        /// Gets information about the current device
        /// </summary>
        DeviceInfo GetDeviceInfo();

        /// <summary>
        /// Configures channels with the specified value range
        /// </summary>
        /// <param name="range">Value range to configure</param>
        void ConfigureChannels(ValueRange range);

        /// <summary>
        /// Scans for available DAQ devices
        /// </summary>
        /// <returns>List of detected devices</returns>
        IList<DeviceInfo> DetectDevices();
    }
}

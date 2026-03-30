using System;
using System.Collections.Generic;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces
{
    /// <summary>
    /// Interface for managing DAQ devices.
    /// HIGH-01 FIX: Removed exposure of concrete SDK types (InstantAoCtrl, InstantDiCtrl, InstantDoCtrl).
    /// Consumers must use abstract methods instead of accessing raw SDK handles.
    /// </summary>
    public interface IDeviceManager
    {
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
        /// Gets the current device type (Analog, Digital, or Unknown)
        /// </summary>
        DeviceType CurrentDeviceType { get; }

        /// <summary>
        /// Initializes the device with the specified device number and profile name
        /// </summary>
        void InitializeDevice(int deviceNumber, string profileName = null);

        /// <summary>
        /// Writes a voltage value to the specified channel (analog devices only)
        /// </summary>
        void WriteVoltage(int channel, double value);

        /// <summary>
        /// Gets information about the current device
        /// </summary>
        DeviceInfo GetDeviceInfo();

        /// <summary>
        /// Configures channels with the specified value range
        /// </summary>
        void ConfigureChannels(Automation.BDaq.ValueRange range);

        /// <summary>
        /// Scans for available DAQ devices
        /// </summary>
        IList<DeviceInfo> DetectDevices();

        /// <summary>
        /// Writes a value to a complete digital port
        /// </summary>
        void WriteDigitalPort(int port, byte value);

        /// <summary>
        /// Reads a complete digital port value
        /// </summary>
        byte ReadDigitalPort(int port);

        /// <summary>
        /// Writes a value to a specific bit of a digital port
        /// </summary>
        void WriteDigitalBit(int port, int bit, bool value);

        /// <summary>
        /// Reads a specific bit from a digital port
        /// </summary>
        bool ReadDigitalBit(int port, int bit);

        /// <summary>
        /// Fast-path for high frequency pulse trains, bypasses logging and error handling.
        /// </summary>
        void WriteDigitalBitFast(int port, int bit, bool value);

        /// <summary>
        /// HIGH-01: Loads an SDK profile file, routing to the correct controller based on device type.
        /// For analog: calls InstantAoCtrl.LoadProfile()
        /// For digital: logs and skips (digital doesn't use SDK profiles)
        /// </summary>
        void LoadProfile(string profilePath);

        /// <summary>
        /// HIGH-01: Gets channel info without exposing SDK types.
        /// Returns false if channel index is invalid or device not ready.
        /// </summary>
        bool TryGetChannelInfo(int channel, out string rangeName);

        /// <summary>
        /// HIGH-01: Resets all output channels to safe defaults (0V or 0x00).
        /// Route-aware: handles both analog and digital devices.
        /// </summary>
        void ResetAllOutputs();
    }
}

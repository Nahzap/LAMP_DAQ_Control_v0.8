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
        /// Initializes the device with the specified device number and profile name
        /// </summary>
        /// <param name="deviceNumber">Device number to initialize</param>
        /// <param name="profileName">Optional profile name to help determine device type</param>
        void InitializeDevice(int deviceNumber, string profileName = null);

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

        /// <summary>
        /// Escribe un valor en un puerto digital completo
        /// </summary>
        /// <param name="port">Número de puerto (0-3)</param>
        /// <param name="value">Valor a escribir (0-255)</param>
        void WriteDigitalPort(int port, byte value);

        /// <summary>
        /// Lee el valor de un puerto digital completo
        /// </summary>
        /// <param name="port">Número de puerto (0-3)</param>
        /// <returns>Valor del puerto (0-255)</returns>
        byte ReadDigitalPort(int port);

        /// <summary>
        /// Escribe un valor en un bit específico de un puerto digital
        /// </summary>
        /// <param name="port">Número de puerto (0-3)</param>
        /// <param name="bit">Número de bit (0-7)</param>
        /// <param name="value">Valor a escribir (true=1, false=0)</param>
        void WriteDigitalBit(int port, int bit, bool value);

        /// <summary>
        /// Lee el valor de un bit específico de un puerto digital
        /// </summary>
        /// <param name="port">Número de puerto (0-3)</param>
        /// <param name="bit">Número de bit (0-7)</param>
        /// <returns>Valor del bit (true=1, false=0)</returns>
        bool ReadDigitalBit(int port, int bit);
    }
}

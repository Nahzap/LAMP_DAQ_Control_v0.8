using System;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Interfaces;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.HAL
{
    /// <summary>
    /// Advantech PCI-1735U implementation of IDigitalHal.
    /// Thin wrapper — no logic, no timers. Pure hardware I/O.
    /// 4 ports × 8 bits = 32 digital channels.
    /// </summary>
    public class AdvantechDigitalHal : IDigitalHal
    {
        private InstantDiCtrl _diCtrl;
        private InstantDoCtrl _doCtrl;
        private readonly ILogger _logger;
        private bool _disposed;
        private readonly byte[] _readBuffer = new byte[4];

        public bool IsReady { get; private set; }

        public AdvantechDigitalHal(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes the digital HAL with the specified Board ID.
        /// Creates both DI and DO controllers for the PCI-1735U.
        /// </summary>
        public bool Initialize(int deviceNumber)
        {
            try
            {
                _diCtrl = new InstantDiCtrl();
                _doCtrl = new InstantDoCtrl();

                int diDevNum = FindDigitalDevice(_diCtrl, deviceNumber);
                int doDevNum = FindDigitalOutputDevice(_doCtrl, deviceNumber);

                bool diOk = false, doOk = false;

                if (diDevNum >= 0)
                {
                    _diCtrl.SelectedDevice = new DeviceInformation(diDevNum);
                    if (_diCtrl.PortCount == 4)
                    {
                        byte dummy;
                        _diCtrl.Read(0, out dummy);
                        diOk = true;
                        _logger.Info($"[DigitalHAL] DI initialized: {_diCtrl.SelectedDevice.Description}");
                    }
                }

                if (doDevNum >= 0)
                {
                    _doCtrl.SelectedDevice = new DeviceInformation(doDevNum);
                    if (_doCtrl.PortCount == 4)
                    {
                        _doCtrl.Write(0, (byte)0);
                        doOk = true;
                        _logger.Info($"[DigitalHAL] DO initialized: {_doCtrl.SelectedDevice.Description}");
                    }
                }

                IsReady = diOk || doOk;
                _logger.Info($"[DigitalHAL] Ready={IsReady} (DI={diOk}, DO={doOk})");
                return IsReady;
            }
            catch (Exception ex)
            {
                _logger.Error("[DigitalHAL] Initialization failed", ex);
                IsReady = false;
                return false;
            }
        }

        public uint ReadInputs()
        {
            if (!IsReady || _diCtrl == null)
                return 0;

            ErrorCode result = _diCtrl.Read(0, 4, _readBuffer);
            if (result != ErrorCode.Success)
                return 0;

            return (uint)(_readBuffer[0] |
                         (_readBuffer[1] << 8) |
                         (_readBuffer[2] << 16) |
                         (_readBuffer[3] << 24));
        }

        public bool ReadInputsRaw(byte[] buffer)
        {
            if (!IsReady || _diCtrl == null || buffer == null || buffer.Length < 4)
                return false;

            ErrorCode result = _diCtrl.Read(0, 4, buffer);
            return result == ErrorCode.Success;
        }

        public void WriteOutputs(uint state)
        {
            if (!IsReady || _doCtrl == null)
                return;

            _doCtrl.Write(0, (byte)(state & 0xFF));
            _doCtrl.Write(1, (byte)((state >> 8) & 0xFF));
            _doCtrl.Write(2, (byte)((state >> 16) & 0xFF));
            _doCtrl.Write(3, (byte)((state >> 24) & 0xFF));
        }

        public void WriteOutputsMasked(uint state, byte portMask)
        {
            if (!IsReady || _doCtrl == null)
                return;

            if ((portMask & 0x01) != 0) _doCtrl.Write(0, (byte)(state & 0xFF));
            if ((portMask & 0x02) != 0) _doCtrl.Write(1, (byte)((state >> 8) & 0xFF));
            if ((portMask & 0x04) != 0) _doCtrl.Write(2, (byte)((state >> 16) & 0xFF));
            if ((portMask & 0x08) != 0) _doCtrl.Write(3, (byte)((state >> 24) & 0xFF));
        }

        private int FindDigitalDevice(InstantDiCtrl ctrl, int boardId)
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

        private int FindDigitalOutputDevice(InstantDoCtrl ctrl, int boardId)
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

                try
                {
                    if (_doCtrl != null)
                    {
                        for (int p = 0; p < 4; p++)
                        {
                            try { _doCtrl.Write(p, (byte)0); } catch { }
                        }
                    }
                }
                catch { }

                _diCtrl?.Dispose();
                _doCtrl?.Dispose();
                _diCtrl = null;
                _doCtrl = null;

                _logger.Info("[DigitalHAL] Disposed");
            }
        }
    }
}

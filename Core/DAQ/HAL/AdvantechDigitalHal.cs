using System;
using System.Threading;
using Automation.BDaq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Exceptions;
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
        private bool _ownsDevices = true;
        private long _errorCount;
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
                        // MED-01 FIX: Explicitly configure port direction for DI
                        // Without this, direction depends on prior hardware state
                        for (int p = 0; p < _diCtrl.Ports.Length; p++)
                        {
                            try { _diCtrl.Ports[p].DirectionMask = 0x00; /* 0x00 = all bits input */ }
                            catch (Exception ex) { _logger.Warn($"[DigitalHAL] Could not set DI port {p} direction: {ex.Message}"); }
                        }
                        
                        byte dummy;
                        var diErr = _diCtrl.Read(0, out dummy);
                        ThrowOnError(diErr, "DI test read port 0");
                        diOk = true;
                        _logger.Info($"[DigitalHAL] DI initialized: {_diCtrl.SelectedDevice.Description}");
                    }
                }

                if (doDevNum >= 0)
                {
                    _doCtrl.SelectedDevice = new DeviceInformation(doDevNum);
                    if (_doCtrl.PortCount == 4)
                    {
                        // MED-01 FIX: Explicitly configure port direction for DO
                        for (int p = 0; p < _doCtrl.Ports.Length; p++)
                        {
                            try { _doCtrl.Ports[p].DirectionMask = 0xFF; /* 0xFF = all bits output */ }
                            catch (Exception ex) { _logger.Warn($"[DigitalHAL] Could not set DO port {p} direction: {ex.Message}"); }
                        }
                        
                        var doErr = _doCtrl.Write(0, (byte)0);
                        ThrowOnError(doErr, "DO test write port 0");
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

        /// <summary>
        /// Initializes the digital HAL from existing SDK controller instances.
        /// Used for sharing SDK handles with DeviceManager (CRIT-01 fix).
        /// The HAL does NOT own these instances and will NOT dispose them.
        /// </summary>
        public void InitializeFromExisting(InstantDiCtrl existingDi, InstantDoCtrl existingDo)
        {
            if (existingDi == null && existingDo == null)
                throw new ArgumentException("At least one of existingDi or existingDo must be non-null");

            _diCtrl = existingDi;
            _doCtrl = existingDo;
            _ownsDevices = false;
            IsReady = true;
            _logger.Info($"[DigitalHAL] Initialized from existing devices (DI={existingDi != null}, DO={existingDo != null})");
        }

        public void WriteOutputs(uint state)
        {
            if (!IsReady || _doCtrl == null)
                return;

            WarnOnError(_doCtrl.Write(0, (byte)(state & 0xFF)), "WriteOutputs port 0");
            WarnOnError(_doCtrl.Write(1, (byte)((state >> 8) & 0xFF)), "WriteOutputs port 1");
            WarnOnError(_doCtrl.Write(2, (byte)((state >> 16) & 0xFF)), "WriteOutputs port 2");
            WarnOnError(_doCtrl.Write(3, (byte)((state >> 24) & 0xFF)), "WriteOutputs port 3");
        }

        public void WriteOutputsMasked(uint state, byte portMask)
        {
            if (!IsReady || _doCtrl == null)
                return;

            if ((portMask & 0x01) != 0) WarnOnError(_doCtrl.Write(0, (byte)(state & 0xFF)), "WriteMasked port 0");
            if ((portMask & 0x02) != 0) WarnOnError(_doCtrl.Write(1, (byte)((state >> 8) & 0xFF)), "WriteMasked port 1");
            if ((portMask & 0x04) != 0) WarnOnError(_doCtrl.Write(2, (byte)((state >> 16) & 0xFF)), "WriteMasked port 2");
            if ((portMask & 0x08) != 0) WarnOnError(_doCtrl.Write(3, (byte)((state >> 24) & 0xFF)), "WriteMasked port 3");
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

        /// <summary>
        /// Checks SDK ErrorCode and throws DAQOperationException on failure.
        /// Used for initialization paths.
        /// </summary>
        private void ThrowOnError(ErrorCode err, string operation)
        {
            if (err != ErrorCode.Success)
            {
                string msg = $"[DigitalHAL] {operation} failed: SDK ErrorCode={err}";
                _logger.Error(msg);
                throw new DAQOperationException(msg);
            }
        }

        /// <summary>
        /// Checks SDK ErrorCode and logs warning on failure (no throw).
        /// Used for hot-path writes to avoid killing engine threads.
        /// Logs at most once per 1000 errors to prevent log flooding.
        /// </summary>
        private void WarnOnError(ErrorCode err, string operation)
        {
            if (err != ErrorCode.Success)
            {
                long count = Interlocked.Increment(ref _errorCount);
                if (count == 1 || count % 1000 == 0)
                {
                    _logger.Warn($"[DigitalHAL] {operation}: SDK ErrorCode={err} (error #{count})");
                }
            }
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

                if (_ownsDevices)
                {
                    _diCtrl?.Dispose();
                    _doCtrl?.Dispose();
                }
                _diCtrl = null;
                _doCtrl = null;

                _logger.Info("[DigitalHAL] Disposed");
            }
        }
    }
}

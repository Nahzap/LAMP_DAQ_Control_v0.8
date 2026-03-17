using Microsoft.VisualStudio.TestTools.UnitTesting;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using System;
using System.Linq;

namespace LAMP_DAQ_Control_v0_8.SignalManager.Tests
{
    /// <summary>
    /// Unit tests for SignalOperations - Pure stateless functions
    /// Tests cover: DetectConflicts, SortByStartTime, FilterByChannel, ValidateAll, CalculateTotalDuration
    /// </summary>
    [TestClass]
    public class SignalOperationsTests
    {
        private SignalTable _table;
        
        [TestInitialize]
        public void Setup()
        {
            _table = new SignalTable(64);
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _table = null;
        }
        
        #region DetectConflicts Tests
        
        [TestMethod]
        public void DetectConflicts_NoOverlap_ReturnsEmptyList()
        {
            // Arrange - Events with no overlap
            _table.AddSignal("E1", 0, 1_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 2_000_000_000, 1_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var conflicts = SignalOperations.DetectConflicts(_table);
            
            // Assert
            Assert.AreEqual(0, conflicts.Count);
        }
        
        [TestMethod]
        public void DetectConflicts_OverlappingEvents_DetectsConflict()
        {
            // Arrange - E1 ends at 2s, E2 starts at 1s (overlap!)
            _table.AddSignal("E1", 0, 2_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 1_000_000_000, 2_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var conflicts = SignalOperations.DetectConflicts(_table);
            
            // Assert
            Assert.AreEqual(1, conflicts.Count);
        }
        
        [TestMethod]
        public void DetectConflicts_DifferentChannels_NoConflict()
        {
            // Arrange - Same time but different channels
            _table.AddSignal("E1", 0, 2_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 0, 2_000_000_000, 1, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var conflicts = SignalOperations.DetectConflicts(_table);
            
            // Assert
            Assert.AreEqual(0, conflicts.Count, "Different channels should not conflict");
        }
        
        [TestMethod]
        public void DetectConflicts_DifferentDevices_NoConflict()
        {
            // Arrange - Same channel but different devices
            _table.AddSignal("E1", 0, 2_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 0, 2_000_000_000, 0, DeviceType.Digital, "PCI-1735U", SignalEventType.DigitalPulse);
            
            // Act
            var conflicts = SignalOperations.DetectConflicts(_table);
            
            // Assert
            Assert.AreEqual(0, conflicts.Count, "Different devices should not conflict");
        }
        
        [TestMethod]
        public void DetectConflicts_WithinTolerance_NoConflict()
        {
            // Arrange - E1 ends at 1.000s, E2 starts at 1.0005s (within 1ms tolerance)
            _table.AddSignal("E1", 0, 1_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 1_000_500_000, 1_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var conflicts = SignalOperations.DetectConflicts(_table);
            
            // Assert
            Assert.AreEqual(0, conflicts.Count, "Events within tolerance should not conflict");
        }
        
        [TestMethod]
        public void DetectConflicts_MultipleConflicts_DetectsAll()
        {
            // Arrange - 3 overlapping events on same channel
            _table.AddSignal("E1", 0, 3_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 1_000_000_000, 3_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E3", 2_000_000_000, 3_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var conflicts = SignalOperations.DetectConflicts(_table);
            
            // Assert
            Assert.IsTrue(conflicts.Count >= 2, "Should detect multiple conflicts");
        }
        
        #endregion
        
        #region SortByStartTime Tests
        
        [TestMethod]
        public void SortByStartTime_UnsortedTable_SortsCorrectly()
        {
            // Arrange - Add in reverse order
            _table.AddSignal("E3", 3_000_000_000, 1000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E1", 1_000_000_000, 1000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 2_000_000_000, 1000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            SignalOperations.SortByStartTime(_table);
            
            // Assert
            Assert.AreEqual("E1", _table.Names[0]);
            Assert.AreEqual("E2", _table.Names[1]);
            Assert.AreEqual("E3", _table.Names[2]);
            Assert.IsTrue(_table.StartTimesNs[0] <= _table.StartTimesNs[1]);
            Assert.IsTrue(_table.StartTimesNs[1] <= _table.StartTimesNs[2]);
        }
        
        [TestMethod]
        public void SortByStartTime_AlreadySorted_RemainsUnchanged()
        {
            // Arrange - Add in sorted order
            _table.AddSignal("E1", 1_000_000_000, 1000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 2_000_000_000, 1000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E3", 3_000_000_000, 1000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            SignalOperations.SortByStartTime(_table);
            
            // Assert
            Assert.AreEqual("E1", _table.Names[0]);
            Assert.AreEqual("E2", _table.Names[1]);
            Assert.AreEqual("E3", _table.Names[2]);
        }
        
        [TestMethod]
        public void SortByStartTime_EmptyTable_DoesNotCrash()
        {
            // Act - Should not throw
            SignalOperations.SortByStartTime(_table);
            
            // Assert
            Assert.AreEqual(0, _table.Count);
        }
        
        [TestMethod]
        public void SortByStartTime_SingleElement_DoesNotCrash()
        {
            // Arrange
            _table.AddSignal("E1", 1_000_000_000, 1000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            SignalOperations.SortByStartTime(_table);
            
            // Assert
            Assert.AreEqual(1, _table.Count);
            Assert.AreEqual("E1", _table.Names[0]);
        }
        
        [TestMethod]
        public void SortByStartTime_PreservesAllColumns()
        {
            // Arrange
            Guid id1 = Guid.NewGuid();
            Guid id2 = Guid.NewGuid();
            _table.AddSignal("E2", 2_000_000_000, 500, 1, DeviceType.Analog, "PCIe-1824", SignalEventType.Ramp, "#FF0000", id2);
            _table.AddSignal("E1", 1_000_000_000, 1000, 0, DeviceType.Digital, "PCI-1735U", SignalEventType.DigitalPulse, "#00FF00", id1);
            
            // Act
            SignalOperations.SortByStartTime(_table);
            
            // Assert - E1 should be first
            Assert.AreEqual(id1, _table.EventIds[0]);
            Assert.AreEqual("E1", _table.Names[0]);
            Assert.AreEqual(1_000_000_000, _table.StartTimesNs[0]);
            Assert.AreEqual(1000, _table.DurationsNs[0]);
            Assert.AreEqual(0, _table.Channels[0]);
            Assert.AreEqual(DeviceType.Digital, _table.DeviceTypes[0]);
            Assert.AreEqual("PCI-1735U", _table.DeviceModels[0]);
            Assert.AreEqual(SignalEventType.DigitalPulse, _table.EventTypes[0]);
            Assert.AreEqual("#00FF00", _table.Colors[0]);
        }
        
        #endregion
        
        #region FilterByChannel Tests
        
        [TestMethod]
        public void FilterByChannel_MatchingEvents_ReturnsIndices()
        {
            // Arrange
            _table.AddSignal("E1", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 2000, 500, 1, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E3", 3000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var indices = SignalOperations.FilterByChannel(_table, 0, DeviceType.Analog, "PCIe-1824");
            
            // Assert
            Assert.AreEqual(2, indices.Length);
            Assert.IsTrue(indices.Contains(0));
            Assert.IsTrue(indices.Contains(2));
        }
        
        [TestMethod]
        public void FilterByChannel_NoMatches_ReturnsEmptyArray()
        {
            // Arrange
            _table.AddSignal("E1", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var indices = SignalOperations.FilterByChannel(_table, 5, DeviceType.Analog, "PCIe-1824");
            
            // Assert
            Assert.AreEqual(0, indices.Length);
        }
        
        [TestMethod]
        public void FilterByChannel_DifferentDevice_ReturnsEmpty()
        {
            // Arrange
            _table.AddSignal("E1", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var indices = SignalOperations.FilterByChannel(_table, 0, DeviceType.Digital, "PCI-1735U");
            
            // Assert
            Assert.AreEqual(0, indices.Length);
        }
        
        #endregion
        
        #region ValidateAll Tests
        
        [TestMethod]
        public void ValidateAll_AllValid_ReturnsNoErrors()
        {
            // Arrange
            int idx = _table.AddSignal("E1", 1_000_000_000, 500_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.Ramp);
            _table.Attributes.SetStartVoltage(idx, 0);
            _table.Attributes.SetEndVoltage(idx, 5);
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.AreEqual(0, errors.Count);
        }
        
        [TestMethod]
        public void ValidateAll_NegativeStartTime_ReturnsError()
        {
            // Arrange
            _table.AddSignal("E1", -1000, 500_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.error.Contains("negative")));
        }
        
        [TestMethod]
        public void ValidateAll_ZeroDuration_ReturnsError()
        {
            // Arrange
            _table.AddSignal("E1", 1_000_000_000, 0, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.error.Contains("Duration")));
        }
        
        [TestMethod]
        public void ValidateAll_InvalidChannel_ReturnsError()
        {
            // Arrange
            _table.AddSignal("E1", 1_000_000_000, 500, 50, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.error.Contains("Channel")));
        }
        
        [TestMethod]
        public void ValidateAll_RampMissingStartVoltage_ReturnsError()
        {
            // Arrange
            int idx = _table.AddSignal("E1", 1_000_000_000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.Ramp);
            // Don't set start voltage
            _table.Attributes.SetEndVoltage(idx, 5);
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.error.Contains("startVoltage")));
        }
        
        [TestMethod]
        public void ValidateAll_RampMissingEndVoltage_ReturnsError()
        {
            // Arrange
            int idx = _table.AddSignal("E1", 1_000_000_000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.Ramp);
            _table.Attributes.SetStartVoltage(idx, 0);
            // Don't set end voltage
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.error.Contains("endVoltage")));
        }
        
        [TestMethod]
        public void ValidateAll_VoltageOutOfRange_ReturnsError()
        {
            // Arrange
            int idx = _table.AddSignal("E1", 1_000_000_000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.Ramp);
            _table.Attributes.SetStartVoltage(idx, -1); // Out of range
            _table.Attributes.SetEndVoltage(idx, 5);
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.error.Contains("0-10V")));
        }
        
        [TestMethod]
        public void ValidateAll_DCMissingVoltage_ReturnsError()
        {
            // Arrange
            _table.AddSignal("E1", 1_000_000_000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            // Don't set voltage
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.error.Contains("voltage")));
        }
        
        [TestMethod]
        public void ValidateAll_WaveformInvalidFrequency_ReturnsError()
        {
            // Arrange
            int idx = _table.AddSignal("E1", 1_000_000_000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.Waveform);
            _table.Attributes.SetWaveformParams(idx, 0, 5, 0); // freq = 0 is invalid
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.error.Contains("frequency")));
        }
        
        [TestMethod]
        public void ValidateAll_WaveformAmplitudePlusOffsetExceeds10V_ReturnsError()
        {
            // Arrange
            int idx = _table.AddSignal("E1", 1_000_000_000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.Waveform);
            _table.Attributes.SetWaveformParams(idx, 100, 6, 5); // 6 + 5 = 11V > 10V
            
            // Act
            var errors = SignalOperations.ValidateAll(_table);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.error.Contains("amplitude + offset")));
        }
        
        #endregion
        
        #region CalculateTotalDuration Tests
        
        [TestMethod]
        public void CalculateTotalDuration_EmptyTable_ReturnsZero()
        {
            // Act
            long duration = SignalOperations.CalculateTotalDuration(_table);
            
            // Assert
            Assert.AreEqual(0, duration);
        }
        
        [TestMethod]
        public void CalculateTotalDuration_SingleEvent_ReturnsEndTime()
        {
            // Arrange
            long start = 1_000_000_000;
            long duration = 500_000_000;
            _table.AddSignal("E1", start, duration, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            long totalDuration = SignalOperations.CalculateTotalDuration(_table);
            
            // Assert
            Assert.AreEqual(start + duration, totalDuration);
        }
        
        [TestMethod]
        public void CalculateTotalDuration_MultipleEvents_ReturnsMaxEndTime()
        {
            // Arrange
            _table.AddSignal("E1", 0, 2_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("E2", 1_000_000_000, 3_000_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC); // Ends at 4s
            _table.AddSignal("E3", 2_000_000_000, 500_000_000, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            long totalDuration = SignalOperations.CalculateTotalDuration(_table);
            
            // Assert
            Assert.AreEqual(4_000_000_000, totalDuration); // E2 ends at 4s
        }
        
        #endregion
    }
}
